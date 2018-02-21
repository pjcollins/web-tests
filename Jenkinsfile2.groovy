#!/bin/groovy
def logParsingRuleFile = ""
def gitCommitHash = ""

def USE_MONO_BRANCH = "NONE"
def USE_XI_BRANCH = "NONE"
def USE_XM_BRANCH = "NONE"
def USE_XA_BRANCH = "NONE"

def profileSetup ()
{
	def profile = "${env.JENKINS_PROFILE}"
	if (profile == 'martin') {
		params.USE_MONO_BRANCH = 'master'
		params.USE_XI_BRANCH = 'NONE'
		params.USE_XM_BRANCH = 'NONE'
		params.USE_XA_BRANCH = 'NONE'
	} else {
		USE_MONO_BRANCH = params.USE_MONO_BRANCH
		USE_XI_BRANCH = params.USE_XI_BRANCH
		USE_XM_BRANCH = params.USE_XM_BRANCH
		USE_XA_BRANCH = params.USE_XA_BRANCH
	}
}

def provision ()
{
	def args = [ ]
	if (USE_MONO_BRANCH != 'NONE' && USE_MONO_BRANCH != '') {
		args << "--mono=${USE_MONO_BRANCH}"
	}
	if (USE_XI_BRANCH != 'NONE' && USE_XI_BRANCH != '') {
		args << "--xi=${USE_XI_BRANCH}"
	}
	if (USE_XM_BRANCH != 'NONE' && USE_XM_BRANCH != '') {
		args << "--xm=${USE_XM_BRANCH}"
	}
	if (USE_XA_BRANCH != 'NONE' && USE_XA_BRANCH != '') {
		args << "--xa=${USE_XA_BRANCH}"
	}
	def summaryFile = "${env.WORKSPACE}/summary.txt"
	def provisionOutput = "provision-output.txt"
	def provisionHtml = "provision-output.html"
	args << "--summary=$summaryFile"
	args << "--out=$provisionOutput"
	args << "--html=$provisionHtml"
	def argList = args.join (" ")
	dir ('web-tests/Tools/AutoProvisionTool') {
		try {
			runShell ("nuget restore AutoProvisionTool.sln")
			runShell ("msbuild AutoProvisionTool.sln")
			withCredentials ([string(credentialsId: 'mono-webtests-github-token', variable: 'JENKINS_OAUTH_TOKEN')]) {
				runShell ("mono --debug ./bin/Debug/AutoProvisionTool.exe $argList provision")
			}
		} finally {
			archiveArtifacts artifacts: provisionOutput, fingerprint: true, allowEmptyArchive: true
			rtp nullAction: '1', parserName: 'html', stableText: "\${FILE:$provisionHtml}"
		}
	}
	
	def summary = readFile summaryFile
	echo "Setting build summary: $summary"
	currentBuild.description = summary
}

def enableMono ()
{
	return USE_MONO_BRANCH != 'NONE' && USE_MONO_BRANCH != ''
}

def enableXI ()
{
	return USE_XI_BRANCH != 'NONE' && USE_XI_BRANCH != ''
}

def enableXM ()
{
	return USE_XM_BRANCH != 'NONE' && USE_XM_BRANCH != ''
}

def enableXA ()
{
	return USE_XA_BRANCH != 'NONE' && USE_XA_BRANCH != ''
}

def runShell (String command)
{
    def dir = pwd()
    echo "SHELL ($dir): $command"
    sh command
}

def build (String targets)
{
	dir ('web-tests') {
		runShell ("msbuild Jenkinsfile.targets /t:MultiBuild /p:JenkinsTargets=$targets")
	}
}

def buildAll ()
{
	def targets = [ ]
	if (enableMono ()) {
		targets << "Console"
		targets << "Console-AppleTls"
		targets << "Console-Legacy"
	}
	if (enableXI ()) {
		targets << "IOS"
	}
	if (enableXM ()) {
		targets << "Mac"
	}
	if (enableXA ()) {
		targets << "Android-Btls"
	}
	
	if (targets.size() == 0) {
		echo "No configurations enabled!"
		currentBuild.result = "NOT_BUILT"
		return
	}
	
	def targetList = targets.join (":")
	build (targetList)
}

def run (String target, String testCategory, String resultOutput, String junitResultOutput, String stdOut, String jenkinsHtml)
{
	def iosParams = "IosRuntime=$IOS_RUNTIME,IosDeviceType=$IOS_DEVICE_TYPE"
	def resultParams = "ResultOutput=$resultOutput,JUnitResultOutput=$junitResultOutput"
	def outputParams = "StdOut=$stdOut,JenkinsHtml=$jenkinsHtml"
	def extraParams = ""
	if (EXTRA_JENKINS_ARGUMENTS != '') {
		def extraParamValue = EXTRA_JENKINS_ARGUMENTS
		extraParams = ",JenkinsExtraArguments=\"$extraParamValue\""
	}
	runShell ("msbuild Jenkinsfile.targets /t:Run /p:JenkinsTarget=$target,TestCategory=$testCategory,$iosParams,$resultParams,$outputParams$extraParams")
}

def runTests (String target, String category, Boolean unstable = false, Integer timeoutValue = 15)
{
	dir ('web-tests') {
		def outputDir = "out/" + target + "/" + category
		def outputDirAbs = pwd() + "/" + outputDir
		sh "mkdir -p $outputDirAbs"
		def resultOutput = "$outputDirAbs/TestResult-${target}-${category}.xml"
		def junitResultOutput = "$outputDirAbs/JUnitTestResult-${target}-${category}.xml"
        def outputLog = "$outputDir/output-${target}-${category}.log"
		def jenkinsHtmlLog = "$outputDir/jenkins-${target}-${category}.html"
		Boolean error = false
		try {
			timeout (timeoutValue) {
				run (target, category, resultOutput, junitResultOutput, outputLog, jenkinsHtmlLog)
			}
		} catch (exception) {
			def result = currentBuild.result
			echo "RUN FAILED: $exception $result $unstable"
			if (unstable) {
				currentBuild.result = "UNSTABLE"
				echo "SETTING TO UNSTABLE"
				error = true
			}
		} finally {
			archiveArtifacts artifacts: "$outputDir/*.log", fingerprint: true, allowEmptyArchive: true
			if (fileExists (jenkinsHtmlLog)) {
				archiveArtifacts artifacts: "$jenkinsHtmlLog", fingerprint: true, allowEmptyArchive: true
				rtp nullAction: '1', parserName: 'html', stableText: "\${FILE:$jenkinsHtmlLog}"
			}
			if (!error) {
				junit keepLongStdio: true, testResults: "$outputDir/*.xml"
				archiveArtifacts artifacts: "$outputDir/*.xml", fingerprint: true
			}
		}
	}
}

node ('master') {
    stage ('initialize') {
        // We need to define this on the master node.
        logParsingRuleFile = "${env.WORKSPACE}/../workspace@script/jenkins-log-parser.txt"
		profileSetup ()
    }
}

node ('felix-25-sierra') {
    try {
        timestamps {
            stage ('checkout') {
                dir ('web-tests') {
                    git url: 'git@github.com:xamarin/web-tests.git', branch: 'work-jenkins'
                    sh 'git clean -xffd'
					gitCommitHash = sh (script: "git log -n 1 --pretty=format:'%h'", returnStdout: true)
					currentBuild.displayName = "#$currentBuild.number:$gitCommitHash"
                }
            }
            stage ('provision') {
				provision ()
            }
            stage ('build') {
                buildAll ()
            }
            if (enableMono ()) {
                stage ('console-work') {
                    runTests ('Console', 'Work')
                }
                stage ('console-new') {
                    runTests ('Console', 'New')
                }
                stage ('console-all') {
                    runTests ('Console', 'All')
                }
                stage ('console-appletls-work') {
                    runTests ('Console-AppleTls', 'Work')
                }
                stage ('console-appletls-new') {
                    runTests ('Console-AppleTls', 'New')
                }
                stage ('console-appletls-all') {
                    runTests ('Console-AppleTls', 'All')
                }
                stage ('console-legacy-work') {
                    runTests ('Console-Legacy', 'Work')
                }
                stage ('console-legacy-new') {
                    runTests ('Console-Legacy', 'New')
                }
                stage ('console-legacy-all') {
                    runTests ('Console-Legacy', 'All')
                }
            }
            if (enableXI ()) {
                stage ('ios-work') {
                    runTests ('IOS', 'Work')
                }
                stage ('ios-new') {
                    runTests ('IOS', 'New')
                }
                stage ('ios-all') {
                    runTests ('IOS', 'All')
                }
            }
            if (enableXM ()) {
                stage ('mac-work') {
                    runTests ('Mac', 'Work')
                }
                stage ('mac-new') {
                    runTests ('Mac', 'New')
                }
                stage ('mac-all') {
                    runTests ('Mac', 'All')
                }
            }
            if (enableXA ()) {
                stage ('android-btls-work') {
                    runTests ('Android-Btls', 'Work', true)
                }
                stage ('android-btls-new') {
                    runTests ('Android-Btls', 'New', true)
                }
                stage ('android-btls-all') {
                    runTests ('Android-Btls', 'All', true, 30)
                }
            }
        }
    } finally {
        stage ('parse-logs') {
            step ([$class: 'LogParserPublisher', parsingRulesPath: "$logParsingRuleFile", useProjectRule: false, failBuildOnError: true]);
        }
    }
}

#!/bin/groovy
def gitCommitHash = ""

def USE_MONO_BRANCH = "NONE"
def USE_XI_BRANCH = "NONE"
def USE_XM_BRANCH = "NONE"
def USE_XA_BRANCH = "NONE"
def IOS_DEVICE_TYPE = "iPhone-5s"
def IOS_RUNTIME = "iOS-10-3"
def EXTRA_JENKINS_ARGUMENTS = ""

def profileSetup ()
{
	def profile = "${env.JENKINS_PROFILE}"
	if (profile == 'master') {
		USE_MONO_BRANCH = 'master'
		USE_XI_BRANCH = 'NONE'
		USE_XM_BRANCH = 'NONE'
		USE_XA_BRANCH = 'NONE'
		EXTRA_JENKINS_ARGUMENTS = 'MARTIN'
		IOS_DEVICE_TYPE = 'iPhone-5s'
		IOS_RUNTIME = "iOS-10-0"
	} else if (profile == '2017-12') {
		USE_MONO_BRANCH = '2017-12'
		USE_XI_BRANCH = 'NONE'
		USE_XM_BRANCH = 'NONE'
		USE_XA_BRANCH = 'NONE'
		IOS_DEVICE_TYPE = 'iPhone-5s'
		IOS_RUNTIME = "iOS-10-0"
	} else if (profile == '2018-02') {
		USE_MONO_BRANCH = '2018-02'
		USE_XI_BRANCH = 'NONE'
		USE_XM_BRANCH = 'NONE'
		USE_XA_BRANCH = 'NONE'
		IOS_DEVICE_TYPE = 'iPhone-5s'
		IOS_RUNTIME = "iOS-10-0"
	} else if (profile == 'macios') {
		USE_MONO_BRANCH = 'NONE'
		USE_XI_BRANCH = 'master'
		USE_XM_BRANCH = 'master'
		USE_XA_BRANCH = 'NONE'
		IOS_DEVICE_TYPE = "iPhone-5s"
		IOS_RUNTIME = "iOS-10-0"
	} else if (profile == 'macios-2018-02') {
		USE_MONO_BRANCH = 'NONE'
		USE_XI_BRANCH = 'mono-2018-02'
		USE_XM_BRANCH = 'mono-2018-02'
		USE_XA_BRANCH = 'NONE'
		IOS_DEVICE_TYPE = "iPhone-5s"
		IOS_RUNTIME = "iOS-10-0"
	} else {
		USE_MONO_BRANCH = params.USE_MONO_BRANCH
		USE_XI_BRANCH = params.USE_XI_BRANCH
		USE_XM_BRANCH = params.USE_XM_BRANCH
		USE_XA_BRANCH = params.USE_XA_BRANCH
		IOS_DEVICE_TYPE = params.IOS_DEVICE_TYPE
		IOS_RUNTIME = params.IOS_RUNTIME
		EXTRA_JENKINS_ARGUMENTS = params.EXTRA_JENKINS_ARGUMENTS
	}
}

def triggerJob ()
{
    def triggeredBuild = build job: 'web-tests-martin4', parameters: [
		string (name: 'USE_MONO_BRANCH', value: USE_MONO_BRANCH),
		string (name: 'USE_XI_BRANCH', value: USE_XI_BRANCH),
		string (name: 'USE_XM_BRANCH', value: USE_XM_BRANCH),
		string (name: 'USE_XA_BRANCH', value: USE_XA_BRANCH),
		string (name: 'IOS_DEVICE_TYPE', value: IOS_DEVICE_TYPE),
		string (name: 'IOS_RUNTIME', value: IOS_RUNTIME),
		string (name: 'EXTRA_JENKINS_ARGUMENTS', value: EXTRA_JENKINS_ARGUMENTS),
	], wait: true, propagate: false
	currentBuild.result = triggeredBuild.result
	echo "Build status: ${currentBuild.result}"
	
	def vars = triggeredBuild.getBuildVariables ()
	currentBuild.description = "${triggeredBuild.displayName} - ${triggeredBuild.description}"
	
	rtp nullAction: '1', parserName: 'html', stableText: "<h2>Downstream build: <a href=\"${triggeredBuild.absoluteUrl}\">${triggeredBuild.displayName}</a></h2><p>${triggeredBuild.description}</p>"
	
	// def summaryBadge = manager.createSummary ('info.gif')
	// summaryBadge.appendText ("<h2>Downstream build: <a href=\"${triggeredBuild.absoluteUrl}\">${triggeredBuild.displayName}</a></h2>", false)
	// summaryBadge.appendText ("<p>${triggeredBuild.description}</p>", false)

	// Unset to avoid a NonSerializableException
	def triggeredId = (''+triggeredBuild.id).split('#')[0]
	triggeredBuild = null
	vars = null
	summaryBadge = null
	
	sh "rm -rf artifacts"
	sh "mkdir -p artifacts"
	
	echo "Copying artifacts."
	
	copyArtifacts projectName: 'web-tests-martin4', selector: specific (triggeredId), target: 'artifacts', flatten: true, fingerprintArtifacts: true
	
	sh "ls -lR artifacts"
	
	def provisionHtml = 'artifacts/provision-output.html'
	if (fileExists (provisionHtml)) {
		rtp nullAction: '1', parserName: 'html', stableText: "\${FILE:$provisionHtml}"
	}
	
	echo "Publishing html summaries."
	
	def htmlFiles = findFiles (glob: 'artifacts/jenkins-summary-*.html')
	for (file in htmlFiles) {
		rtp nullAction: '1', parserName: 'html', stableText: "\${FILE:$file}"
	}
	
	echo "Done publishing html summaries."
	
	junit keepLongStdio: true, testResults: "artifacts/*.xml"

	echo "Done publishing test results."
}

def slackSend (String color, String message)
{
	slackSend channel: "#martin-jenkins", color: color, message: "${currentBuild.displayName}: ${message} (<${env.BUILD_URL}|Open>)\n${currentBuild.description}"
}

node ('felix-25-sierra') {
	timestamps {
		stage ('initialize') {
			profileSetup ()
		}
		stage ('build') {
			try {
				triggerJob ()
				if (currentBuild.result == "SUCCESS") {
					slackSend ("good", "Success")
				} else {
					slackSend ("danger", "${currentBuild.result}")
				}
			} catch (exception) {
				slackSend ("danger", "ERROR: $exception")
			}
		}
	}
}

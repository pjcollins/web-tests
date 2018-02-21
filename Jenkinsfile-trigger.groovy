#!/bin/groovy
def logParsingRuleFile = ""
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
		USE_MONO_BRANCH = 'NONE'
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
	
	def vars = triggeredBuild.getBuildVariables ()
	currentBuild.description = "${triggeredBuild.displayName} - ${triggeredBuild.description}"
	
	def summaryBadge = manager.createSummary ('info.gif')
	summaryBadge.appendText ("<h2>Downstream build: <a href=\"${triggeredBuild.absoluteUrl}\">${triggeredBuild.displayName}</a></h2>", false)
	summaryBadge.appendText ("<p>${triggeredBuild.description}</p>", false)
	
	echo "COPY ARTIFACTS: ${triggeredBuild.id}"
	
	copyArtifacts projectName: 'web-tests-martin4', selector: specific(triggeredBuild.id), fingerprintArtifacts: true
}

def slackSend ()
{
	slackSend channel: "#martin-jenkins", message: "${env.JOB_NAME} - #${env.BUILD_NUMBER} ${currentBuild.result} (<${env.BUILD_URL}|Open>)"
}

node ('felix-25-sierra') {
    try {
        timestamps {
            stage ('initialize') {
				profileSetup ()
			}
            stage ('build') {
				slackSend ()
				triggerJob ()
            }
			stage ('test') {
				sh 'pwd'
				sh 'ls -lR'
			}
        }
    } finally {
        stage ('parse-logs') {
            step ([$class: 'LogParserPublisher', parsingRulesPath: "$logParsingRuleFile", useProjectRule: false, failBuildOnError: true]);
        }
    }
}

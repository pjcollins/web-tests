#!/bin/groovy
def logParsingRuleFile = ""
def gitCommitHash = ""

def MASTER_JOB = "web-tests-martin4"

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
	} else if (profile == '2017-12') {
		USE_MONO_BRANCH = '2017-12'
		USE_XI_BRANCH = 'NONE'
		USE_XM_BRANCH = 'NONE'
		USE_XA_BRANCH = 'NONE'
	} else if (profile == '2018-02') {
		USE_MONO_BRANCH = '2018-02'
		USE_XI_BRANCH = 'NONE'
		USE_XM_BRANCH = 'NONE'
		USE_XA_BRANCH = 'NONE'
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
    build job: 'web-tests-martin4', parameters: [
		[$class: 'StringParameterValue', name: 'USE_MONO_BRANCH', value: USE_MONO_BRANCH]
	]
}

node ('felix-25-sierra') {
    try {
        timestamps {
            stage ('initialize') {
				profileSetup ()
			}
            stage ('build') {
				triggerJob ()
            }
        }
    } finally {
        stage ('parse-logs') {
            step ([$class: 'LogParserPublisher', parsingRulesPath: "$logParsingRuleFile", useProjectRule: false, failBuildOnError: true]);
        }
    }
}

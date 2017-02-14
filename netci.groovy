// Groovy Script: http://www.groovy-lang.org/syntax.html
// Jenkins DSL: https://github.com/jenkinsci/job-dsl-plugin/wiki

import jobs.generation.Utilities;

static getJobName(def opsysName, def configName) {
  return "${opsysName}_${configName}"
}

static addArchival(def job, def filesToArchive, def filesToExclude) {
  def doNotFailIfNothingArchived = false
  def archiveOnlyIfSuccessful = false

  Utilities.addArchival(job, filesToArchive, filesToExclude, doNotFailIfNothingArchived, archiveOnlyIfSuccessful)
}

static addGithubPRTriggerForBranch(def job, def branchName, def jobName) {
  def prContext = "prtest/${jobName.replace('_', '/')}"
  def triggerPhrase = "(?i)^\\s*(@?dotnet-bot\\s+)?(re)?test\\s+(${prContext})(\\s+please)?\\s*\$"
  def triggerOnPhraseOnly = true

  Utilities.addGithubPRTriggerForBranch(job, branchName, prContext, triggerPhrase, triggerOnPhraseOnly)
}

static addGithubPRCommitStatusForBranch(def job, def branchName, def jobName) {
  def prContext = "prtest/${jobName.replace('_', '/')}"

  job.with {
    wrappers {
      downstreamCommitStatus {
        context(prContext)
      }
    }
  }
}

static addXUnitDotNETResults(def job, def configName) {
  def resultFilePattern = "**/artifacts/${configName}/TestResults/xUnit-*.xml"
  def skipIfNoTestFiles = false
    
  Utilities.addXUnitDotNETResults(job, resultFilePattern, skipIfNoTestFiles)
}

static addExtendedEmailPublisher(def job) {
  job.with {
    publishers {
      extendedEmail {
        defaultContent('$DEFAULT_CONTENT')
        defaultSubject('$DEFAULT_SUBJECT')
        recipientList('$DEFAULT_RECIPIENTS, cc:tomat@microsoft.com')
        triggers {
          aborted {
            content('$PROJECT_DEFAULT_CONTENT')
            sendTo {
              culprits()
              developers()
              recipientList()
              requester()
            }
            subject('$PROJECT_DEFAULT_SUBJECT')
          }
          failure {
            content('$PROJECT_DEFAULT_CONTENT')
            sendTo {
              culprits()
              developers()
              recipientList()
              requester()
            }
            subject('$PROJECT_DEFAULT_SUBJECT')
          }
        }
      }
    }
  }
}

static addBuildSteps(def job, def projectName, def opsysName, def configName, def isPR, def filesToArchive, def filesToExclude) {
  def buildJobName = getJobName(opsysName, configName)
  def buildFullJobName = Utilities.getFullJobName(projectName, buildJobName, isPR)

  def officialSwitch = ''

  if (!isPR) {
    officialSwitch = '-official'
  }

  job.with {
    steps {
      copyArtifacts(buildFullJobName) {
        includePatterns(filesToArchive)
        excludePatterns(filesToExclude)
        buildSelector {
          upstreamBuild {
            fallbackToLastSuccessful(false)
          }
        }
      }
      batchFile(""".\\CIBuild.cmd -configuration ${configName} ${officialSwitch}""")
    }
  }
}

[true, false].each { isPR ->
  ['windows'].each { opsysName ->
    ['debug', 'release'].each { configName ->
      def projectName = GithubProject

      def branchName = GithubBranchName

      def filesToArchive = "**/artifacts/${configName}/**"
      def filesToExclude = "**/artifacts/${configName}/obj/**"

      def jobName = getJobName(opsysName, configName)
      def fullJobName = Utilities.getFullJobName(projectName, jobName, isPR)
      def myJob = job(fullJobName)

      Utilities.standardJobSetup(myJob, projectName, isPR, "*/${branchName}")

      if (isPR) {
        addGithubPRTriggerForBranch(myJob, branchName, jobName)
      } else {
        Utilities.addGithubPushTrigger(myJob)
      }
      
      addArchival(myJob, filesToArchive, filesToExclude)
      addXUnitDotNETResults(myJob, configName)

      Utilities.setMachineAffinity(myJob, 'Windows_NT', 'latest-or-auto-dev15-rc')

      if (!isPR) {
        addExtendedEmailPublisher(myJob)
      }

      addBuildSteps(myJob, projectName, opsysName, configName, isPR, filesToArchive, filesToExclude)
    }
  }
}
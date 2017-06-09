#!/usr/bin/python

settingsFilename  = "CoderCards/local.settings.json"
containerContent  = "content"
indexHtmlFile     = "CoderCardsClient/index.html"

def runCommand(str):
    return subprocess.run(str, stdout=subprocess.PIPE, shell=True).stdout.decode('utf-8').rstrip()

def str2bool(v):
    return v.lower() in ("yes", "true", "t", "1")

import fileinput, re, sys, getopt, subprocess

if (len(sys.argv) < 3):
   print("Usage: {} <storage account name> <storage account resource group> <isLocal>".format(sys.argv[0]))
   sys.exit();

storageName    = sys.argv[1]       # storage account name 
resourceGroup  = sys.argv[2]       # storage resource group
isLocal        = str2bool(sys.argv[3]) # local or prod containers

containerInput    = "input-local"  if isLocal else "card-input"
containerOutput   = "output-local" if isLocal else "card-output"
queueName         = "local-queue"  if isLocal else "input-queue"

# Retrieve the Storage Account connection string 
connstr = runCommand('az storage account show-connection-string --name {} --resource-group {} --query connectionString --output tsv'.format(storageName, resourceGroup))

# get account URL
accountUrl = \
    runCommand('az storage account show --name {} -g {} --output tsv --query "{{primaryEndpoints:primaryEndpoints}}.primaryEndpoints.blob"'.format(storageName, resourceGroup))

# create containers
runCommand('az storage container create --connection-string "{}" --name {}'.format(connstr, containerInput))
runCommand('az storage container create --connection-string "{}" --name {}'.format(connstr, containerOutput))
runCommand('az storage container create --connection-string "{}" --name {}'.format(connstr, containerContent))

# get SAS token for input container
sasToken = runCommand('az storage container generate-sas --connection-string "{}" --name {} --permissions lrw --expiry 2018-01-01 -o tsv'.format(connstr, containerInput))

# set permissions on output and content containers
runCommand('az storage container set-permission --connection-string "{}" --public-access blob -n {}'.format(connstr, containerOutput))
runCommand('az storage container set-permission --connection-string "{}" --public-access blob -n {}'.format(connstr, containerContent))

# upload index.html to storage
runCommand('az storage blob upload --connection-string {} --container-name {} -f {} -n index.html  --content-type "text/html"'.format(connstr, containerContent, indexHtmlFile))

# set CORS on blobs
runCommand('az storage cors add --connection-string "{}" --origins "*" --methods GET PUT OPTIONS --allowed-headers "*" --exposed-headers "*" --max-age 200 --services b'.format(connstr))


# new settings values
settingAzureWebJobsStorage  = '"AzureWebJobsStorage": "{}"'.format(connstr)
settingStorageUrl           = '"STORAGE_URL": "{}"'.format(accountUrl)
settingContainerSas         = '"CONTAINER_SAS": "?{}"'.format(sasToken)

# write out new settings values
print(settingAzureWebJobsStorage)
print(settingStorageUrl)
print(settingContainerSas)

# write changes to file
with open(settingsFilename, 'r') as file:
    filedata = file.read()

filedata = filedata.replace('"AzureWebJobsStorage": ""', settingAzureWebJobsStorage) \
                   .replace('"STORAGE_URL": ""',         settingStorageUrl) \
                   .replace('"CONTAINER_SAS": ""',       settingContainerSas)

filedata = re.sub(r'"input-container": .*,',  '"input-container": "{}",'.format(containerInput), filedata)
filedata = re.sub(r'"output-container": .*,', '"output-container": "{}",'.format(containerOutput), filedata)
filedata = re.sub(r'"input-queue": .*,',      '"input-queue": "{}",'.format(queueName), filedata)

with open(settingsFilename, 'w') as file:
    file.write(filedata)

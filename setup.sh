#/bin/bash

if [ "$#" -ne 2 ]
then
  echo "Usage: setup.sh <storage account name> <storage account resource group>"
  exit 1
fi

# Variables
storageName=$1   # storage account name 
resourceGroup=$2 # storage resource group

# Retrieve the Storage Account connection string 
connstr=$(az storage account show-connection-string --name $storageName --resource-group $resourceGroup --query connectionString --output tsv)

# write connection string to settings file
sed -i 's,"AzureWebJobsStorage": "","AzureWebJobsStorage": "'$connstr'",g' CoderCards/local.settings.json

# write storage account URL to settings file
accountUrl=$(az storage account show --name $storageName -g $resourceGroup --query "{primaryEndpoints:primaryEndpoints}.primaryEndpoints.blob" --output tsv)

sed -i 's,"STORAGE_URL": "","STORAGE_URL": "'$accountUrl'",g' CoderCards/local.settings.json

# create input and output containers
az storage container create --connection-string $connstr -n input-local
az storage container create --connection-string $connstr -n output-local

# get SAS token for input-local container
sasToken=$(az storage container generate-sas --connection-string $connstr -n input-local --permissions lrw --expiry 2018-01-01 -o tsv)

# write SAS token to settings file, using bash replacement expression to escape '&'
sed -i 's,"CONTAINER_SAS": "","CONTAINER_SAS": "?'${sasToken//&/\\&}'",g' CoderCards/local.settings.json

az storage container set-permission --connection-string $connstr --public-access blob -n output-local

# set CORS on blobs
az storage cors add --connection-string $connstr --origins '*' --methods GET PUT OPTIONS --allowed-headers '*' --exposed-headers '*' --max-age 200 --services b
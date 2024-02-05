Install-Module Az
Install-Module CosmosDB

Import-Module Az
Import-Module CosmosDB

param {
    [System.String]
    $TemplateFile,

    [System.String]
    $CosmosAccount,

    [System.String]
    $CosmosDatabase,

    [System.String]
    $CosmosContainer,

    [System.String]
    $CosmosKey,

    [System.String]
    $FrontdoorName,

    [System.String]
    $ResourceGroupname
}


$cosmosContext = New-CosmosDbContext -Account $CosmosAccount -Database $CosmosDatabase -ResourceGroupName $ResourceGroupname
$continuationToken = $null
$OriginDeployments = [System.Collections.ArrayList]::new()
do {
    $responseHeader = $null
    $getCosmosItemsParams = @{
        Context = $cosmosContext
        CollectionId = $CosmosContainer
        ResponseHeader = ([ref] $responseHeader)
        ReturnJson = $true
    }

    if ($continuationToken) {
        $getCosmosItemsParams.ContinuationToken = $continuationToken
    }

    $newDocuments = Get-CosmosDbDocument @getCosmosItemsParams | ConvertFrom-Json -Depth 9
    foreach ($document in $newDocuments.Documents) {
        $originDeployment = @{
            appId = $document.id
            hostname = $document.hostname
            httpPort = $document.httpPort
            httpsPort = $document.httpsPort
        }
    }
    $OriginDeployments.Add($originDeployment)
    $continuationToken = Get-CosmosDbContinuationToken -ResponseHeader $responseHeader
} while (-not [System.String]::IsNullOrEmpty($continuationToken))


$deployParameters = @{
    FrontdoorName = $FrontdoorName
    appOrigins = $OriginDeployments
}
New-AzResourceGroupDeployment -ResourceGroupName $ResourceGroupname -TemplateFile $TemplateFile -TemplateParameterObject $deployParameters



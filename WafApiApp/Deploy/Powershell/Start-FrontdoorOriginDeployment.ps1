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
    $Frontdoor,

    [System.String]
    $Keyvault,

    [System.String]
    $ResourceGroup
}

$cosmosContext = New-CosmosDbContext -Account $CosmosAccount -Database $CosmosDatabase -ResourceGroupName $ResourceGroup
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
    FrontdoorName = $Frontdoor
    appOrigins = $OriginDeployments
}
New-AzResourceGroupDeployment -ResourceGroupName $ResourceGroup -TemplateFile $TemplateFile -TemplateParameterObject $deployParameters



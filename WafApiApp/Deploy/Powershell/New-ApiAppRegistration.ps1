Install-Module Microsoft.Graph
Import-Module Microsoft.Graph

# Create app reg
Connect-MgGraph -Scopes "Application.ReadWrite.All" -TenantId '4c244900-c482-4688-b4c5-a726bc7e05d7' 
$publicClientApplication = @{
    RedirectUris=@("http://localhost:5031")
}

$identifierUris = @("api://api-service")

$apiApplication = @{
    Oauth2PermissionScopes = @(
        @{
            Id=$(New-Guid)
            Value="full"
            AdminConsentDescription="Full Access"
            AdminConsentDisplayName="Full Access"
            IsEnabled=$true
            Type="User"
            UserConsentDescription="Full Access"
            UserConsentDisplayName="Full Access"
        }
    )
}

$appRoles = @(
    @{
        Id=$(New-Guid)
        Value="user"
        DisplayName="Natural users"
        Description="Users can use get requests"
        AllowedMemberTypes=@("User")
    }
    @{
        Id=$(New-Guid)
        Value="spn"
        DisplayName="SPN"
        Description="Service principals can access all endpoints"
        AllowedMemberTypes=@("Application")
    }
)

$application = New-MgApplication -DisplayName 'ApiService' -SignInAudience 'AzureADMyOrg' -PublicClient $publicClientApplication `
    -IdentifierUris $identifierUris ` -Api $apiApplication -AppRoles $appRoles 

$servicePrincipalId= @{ "AppId" = $application.AppId }
New-MgServicePrincipal -BodyParameter $servicePrincipalId


# Assign roles
Connect-MgGraph -Scopes "AppRoleAssignment.ReadWrite.All,User.ReadWrite.All" -TenantId '4c244900-c482-4688-b4c5-a726bc7e05d7' 
$application = Get-MgApplication -Filter "DisplayName eq 'ApiService'"

$userAppRoleId = $($application.AppRoles | where {$_.value -eq "user"}).Id
$spnAppRoleId = $($application.AppRoles | where {$_.value -eq "spn"}).Id

$servicePrincipal = Get-MgServicePrincipal -Filter "DisplayName eq 'ApiService'"
$userId = "62ac247c-3e36-441a-a7f4-11b3e02653f8"
$spnId = "50bd988d-f6ee-4724-ad29-aa4fdcc765ac"
New-MgUserAppRoleAssignment `
  -UserId $userId `
  -AppRoleId $userAppRoleId `
  -PrincipalId $userId `
  -ResourceId $servicePrincipal.Id
New-MgUserAppRoleAssignment `
  -UserId $spnId `
  -AppRoleId $spnAppRoleId `
  -PrincipalId $spnId `
  -ResourceId $servicePrincipal.Id
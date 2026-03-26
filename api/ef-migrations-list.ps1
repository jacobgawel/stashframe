$ErrorActionPreference = 'Stop'

Push-Location $PSScriptRoot
try {
    dotnet ef migrations list `
        --project JSG.API.Stashframe.Core `
        --startup-project JSG.API.Stashframe
}
finally {
    Pop-Location
}

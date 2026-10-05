#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('http://127.0.0.1:5011')]
    [string] $BaseUrl = 'http://127.0.0.1:5011',
    [Parameter(Mandatory)]
    [ValidateRange(1, [long]::MaxValue)]
    [long] $DraftRecipeId
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Smoke([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw [System.ApplicationException]::new($Message) }
}

function Get-Response([string] $Path) {
    # No credentials, bearer token, test-user header or HTTP redirects.
    return Invoke-WebRequest -Uri ($BaseUrl + $Path) -Method Get -TimeoutSec 15 -MaximumRedirection 0 -SkipHttpErrorCheck
}

try {
    $health = Get-Response '/health'
    Assert-Smoke ($health.StatusCode -eq 200 -and $health.Content.Trim() -eq 'Healthy') 'Health/liveness check failed.'
    $response = Get-Response '/api/ingredients'
    Assert-Smoke ($response.StatusCode -eq 200) 'Ingredient catalog request failed.'
    $ingredients = @($response.Content | ConvertFrom-Json)
    $expectedNames = @('Domates', 'Yumurta', 'Mercimek', 'Süt', 'Zeytinyağı', 'Tuz')
    foreach ($name in $expectedNames) {
        Assert-Smoke (@($ingredients | Where-Object Name -CEQ "[SKA-DEV-SEED] $name").Count -eq 1) "Canonical ingredient missing/duplicated: $name"
    }
    Assert-Smoke (@($ingredients | Where-Object Name -CEQ '[SKA-DEV-SEED] Pasif test malzemesi').Count -eq 0) 'Inactive fixture leaked into catalog.'
    $response = Get-Response '/api/ingredients?search=Domates'
    Assert-Smoke ($response.StatusCode -eq 200) 'Ingredient search failed.'
    $search = @($response.Content | ConvertFrom-Json)
    Assert-Smoke (@($search | Where-Object Name -CEQ '[SKA-DEV-SEED] Domates').Count -eq 1) 'Search did not find canonical tomato.'

    $response = Get-Response '/api/recipes'
    Assert-Smoke ($response.StatusCode -eq 200) 'Recipe catalog request failed.'
    $recipes = @($response.Content | ConvertFrom-Json)
    foreach ($title in @('Domatesli Yumurta', 'Mercimek Çorbası')) {
        $canonical = @($recipes | Where-Object Title -CEQ "[SKA-DEV-SEED] $title")
        Assert-Smoke ($canonical.Count -eq 1) "Canonical recipe missing/duplicated: $title"
        $response = Get-Response "/api/recipes/$($canonical[0].Id)"
        Assert-Smoke ($response.StatusCode -eq 200) 'Published recipe detail failed.'
        $detail = $response.Content | ConvertFrom-Json
        Assert-Smoke (@($detail.Steps).Count -eq 2) 'Canonical recipe steps missing.'
        if ($title -eq 'Domatesli Yumurta') {
            Assert-Smoke ($detail.BaseServings -eq 2 -and @($detail.Ingredients).Count -eq 4) 'Tomato recipe graph is invalid.'
            Assert-Smoke ($detail.Ingredients[0].Name -ceq '[SKA-DEV-SEED] Domates' -and
                [decimal]$detail.Ingredients[0].Quantity -eq 0.25 -and $detail.Ingredients[0].Unit -ceq 'kg') 'Mass conversion/order failed.'
            Assert-Smoke ($detail.Ingredients[1].Name -ceq '[SKA-DEV-SEED] Yumurta' -and
                [decimal]$detail.Ingredients[1].Quantity -eq 2 -and $detail.Ingredients[1].Unit -ceq 'adet') 'Count conversion/order failed.'
            Assert-Smoke ($detail.Steps[0].TimerSeconds -eq 120 -and
                $detail.Steps[0].Instruction -ceq 'Domatesleri doğrayıp zeytinyağı ile tavaya alın.') 'Tomato recipe first step/timer is invalid.'
            Assert-Smoke ($detail.Steps[1].TimerSeconds -eq 180 -and
                $detail.Steps[1].Instruction -ceq 'Yumurtaları ekleyip pişirin; isteğe göre tuz ekleyin.') 'Tomato recipe second step/timer is invalid.'
        }
        else {
            Assert-Smoke ($detail.BaseServings -eq 4 -and @($detail.Ingredients).Count -eq 3) 'Soup recipe graph is invalid.'
            Assert-Smoke ($detail.Ingredients[1].Name -ceq '[SKA-DEV-SEED] Süt' -and
                [decimal]$detail.Ingredients[1].Quantity -eq 0.5 -and $detail.Ingredients[1].Unit -ceq 'l') 'Volume conversion/order failed.'
            Assert-Smoke ($detail.Steps[0].TimerSeconds -eq 1200 -and
                $detail.Steps[0].Instruction -ceq 'Yıkanmış mercimeği yeterli su ile yumuşayana kadar pişirin.') 'Soup recipe first step/timer is invalid.'
            Assert-Smoke ($detail.Steps[1].TimerSeconds -eq 120 -and
                $detail.Steps[1].Instruction -ceq 'Süt ve tuzu ekleyip karıştırın; kıvamı su ile ayarlayın.') 'Soup recipe second step/timer is invalid.'
        }
    }
    Assert-Smoke (@($recipes | Where-Object Title -CEQ '[SKA-DEV-SEED] Taslak tarif').Count -eq 0) 'Draft fixture leaked into catalog.'
    Assert-Smoke ((Get-Response "/api/recipes/$DraftRecipeId").StatusCode -eq 404) 'Draft detail must return 404.'
    foreach ($path in @('/api/pantry', '/api/recipes/suitability')) {
        Assert-Smoke ((Get-Response $path).StatusCode -eq 401) 'Protected endpoint did not require authentication.'
    }
    Write-Host 'PASS: liveness, SQL-backed catalog/search/details, Draft exclusion and anonymous auth boundaries. Unrelated catalog records are allowed.'
}
catch {
    $message = if ($_.Exception -is [System.ApplicationException]) { $_.Exception.Message }
    else { 'Request or response parsing failed; response bodies and internal details are suppressed.' }
    Write-Host "FAIL: $message" -ForegroundColor Red
    exit 1
}

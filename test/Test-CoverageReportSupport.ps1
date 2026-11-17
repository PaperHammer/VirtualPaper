$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/TestReportSupport.ps1"

function Require([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }
function Reject([scriptblock] $Action) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    Require $rejected 'Invalid or incomplete coverage must be rejected.'
}
$results = [IO.Path]::GetFullPath("$PSScriptRoot/../TestResults")
$fixture = Join-Path $results ('coverage-report-' + [guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $fixture)
try {
    $specs = @(Get-ProjectTestSpecs | Where-Object Coverage)
    for ($i = 0; $i -lt $specs.Count; $i++) {
        $directory = Join-Path $fixture $specs[$i].Artifact
        [void](New-Item -ItemType Directory -Path $directory)
        $source = switch ($i % 4) {
            0 { 'D:/a/repo/src/VirtualPaper.Alpha/Shared.cs' }
            1 { 'E:\repos\src\virtualpaper.alpha\shared.cs' }
            2 { 'src/VirtualPaper.Alpha/../VirtualPaper.Alpha/Shared.cs' }
            3 { 'VirtualPaper.Alpha/Shared.cs' }
        }
        $sources = if ($i % 4 -eq 3) { '<sources><source>/home/runner/repo/src</source></sources>' } else { '' }
        $hit1 = [int]($i -eq 1); $hit2 = [int]($i -eq 0); $hit3 = [int]($i -eq 2)
        $covered = $hit1 + $hit2 + $hit3
        $xml = @"
<coverage lines-covered="$covered" lines-valid="5">$sources<packages>
  <package name="VirtualPaper.AssemblyAlias"><classes>
    <class name="First" filename="$source"><methods><method><lines><line number="99" hits="1"/></lines></method></methods><lines>
      <line number="1" hits="$hit1"/><line number="2" hits="$hit2"/>
    </lines></class>
    <class name="Closure" filename="$source"><lines><line number="2" hits="0"/></lines></class>
    <class name="Other" filename="src/VirtualPaper.Alpha/Other.cs"><lines><line number="1" hits="0"/></lines></class>
  </classes></package>
  <package name="VirtualPaper.Beta"><classes><class filename="src/VirtualPaper.Beta/Shared.cs"><lines><line number="1" hits="$hit3"/></lines></class></classes></package>
</packages></coverage>
"@
        [IO.File]::WriteAllText((Join-Path $directory 'coverage.cobertura.xml'), $xml)
        # VSTest also retains copies under In. They must never count as another report.
        [void](New-Item -ItemType Directory -Path (Join-Path $directory 'In/staging'))
        [IO.File]::WriteAllText((Join-Path $directory 'In/staging/copy.cobertura.xml'), '<invalid/>')
    }
    $rows = @(Get-TestCoverageRows $fixture)
    Require ($rows.Count -eq 2) 'Group by authored product module, not test project or loaded assembly alias.'
    $alpha = $rows | Where-Object Module -EQ 'VirtualPaper.Alpha'
    $beta = $rows | Where-Object Module -EQ 'VirtualPaper.Beta'
    Require ($alpha.Covered -eq 2 -and $alpha.Valid -eq 3) 'Union hits across reports/classes, normalize roots/case/separators/dot segments and ignore repeated method nodes.'
    Require ($beta.Covered -eq 1 -and $beta.Valid -eq 1) 'The same basename/line in another module is a different source line.'
    Require (($rows | Measure-Object Covered -Sum).Sum -eq 3 -and ($rows | Measure-Object Valid -Sum).Sum -eq 4) 'Merged totals must count each source line exactly once.'
    $path = Join-Path $fixture "$($specs[0].Artifact)/coverage.cobertura.xml"
    $original = [IO.File]::ReadAllText($path)
    foreach ($invalid in @(
        $original.Replace('lines-covered="1"', 'lines-covered="-1"'),
        $original.Replace('lines-valid="5"', 'lines-valid="invalid"'),
        $original.Replace('lines-covered="1"', 'lines-covered="6"'),
        $original.Replace('name="VirtualPaper.AssemblyAlias"', 'name=""'),
        $original.Replace('filename="D:/a/repo/src/VirtualPaper.Alpha/Shared.cs"', 'filename=""'),
        $original.Replace('number="1"', 'number="0"'),
        $original.Replace('number="1"', 'number="invalid"'),
        $original.Replace('hits="0"', 'hits="-1"'),
        $original.Replace('hits="0"', 'hits="invalid"'),
        '<coverage lines-covered="1" lines-valid="5"/>',
        '<broken'
    )) {
        [IO.File]::WriteAllText($path, $invalid)
        Reject { Get-TestCoverageRows $fixture }
    }
    [IO.File]::WriteAllText($path, '<coverage lines-covered="0" lines-valid="0"><packages><package name="VirtualPaper.Empty"><classes><class filename="src/VirtualPaper.Empty/Empty.cs"><lines/></class></classes></package></packages></coverage>')
    $rows = @(Get-TestCoverageRows $fixture)
    $empty = $rows | Where-Object Module -EQ 'VirtualPaper.Empty'
    Require ($empty.Covered -eq 0 -and $empty.Valid -eq 0) 'An empty module must not invent covered source lines.'
    [IO.File]::WriteAllText($path, $original)
    $extra = Join-Path (Split-Path $path) 'extra.cobertura.xml'
    [IO.File]::WriteAllText($extra, $original)
    Reject { Get-TestCoverageRows $fixture }
    Remove-Item -LiteralPath $extra
    Move-Item -LiteralPath $path -Destination "$path.held"
    try { Reject { Get-TestCoverageRows $fixture } } finally { Move-Item -LiteralPath "$path.held" -Destination $path }
} finally {
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($fixture)) -cne $results) { throw 'Unsafe coverage fixture cleanup.' }
    Remove-Item -LiteralPath $fixture -Recurse -Force
}
Write-Host '[OK] Product-module coverage merges source-line hits across reports and rejects missing/malformed evidence.'

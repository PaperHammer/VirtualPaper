#requires -Version 5.1
# Compatibility entry point for the existing UI-only generation command.
[CmdletBinding()]
param([string]$SourceRoot, [string]$MappingFile, [string]$XamlRoot,
    [Parameter(Mandatory)][string]$OutputRoot, [switch]$Check)
& (Join-Path $PSScriptRoot '../../../localization/Generate-Resources.ps1') -Target UI @PSBoundParameters

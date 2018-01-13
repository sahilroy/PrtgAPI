﻿. $PSScriptRoot\Support\UnitTest.ps1

Describe "Get-Probe" -Tag @("PowerShell", "UnitTest") {

    It "can deserialize" {
        $probes = Get-Probe
        $probes.Count | Should Be 1
    }

    It "can filter by status" {
        $items = (GetItem),(GetItem),(GetItem),(GetItem)
        $items.Count | Should Be 4

        $items[0].StatusRaw = "5" # Down
        $items[1].StatusRaw = "3" # Up
        $items[2].StatusRaw = "3" # Up
        $items[3].StatusRaw = "8" # PausedByDependency

        WithItems $items {
            $probes = Get-Probe -Status Up,Paused

            $probes.Count | Should Be 3
        }
    }

    It "can filter valid wildcards" {
        $obj1 = GetItem
        $obj2 = GetItem

        $obj2.Tags = "testbananatest"

        WithItems ($obj1, $obj2) {
            $probes = Get-Probe -Tags *banana*
            $probes.Count | Should Be 1
        }
    }

    It "can ignore invalid wildcards" {
        $obj1 = GetItem
        $obj2 = GetItem

        $obj2.Tags = "testbananatest"

        WithItems ($obj1, $obj2) {
            $probes = Get-Probe -Tags *apple*
            $probes.Count | Should Be 0
        }
    }
}

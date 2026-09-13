// Downloads CSV exports (full history) for ALL Alior accounts into c:\git\alior-scrape.
// Credentials come from env vars ALIOR_USERNAME / ALIOR_PASSWORD.
// Run: dotnet fsi C:/git/clients/scripts/scrape-all.fsx

#r "nuget: FSharp.Data, 6.6"
#r "nuget: PuppeteerSharp, 20.2.2"

#load "../src/Utils.fs"
#load "../src/Alior.fs"

open System
open System.IO
open Utils
open Alior

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__ // error snapshots land in scripts/snapshots (gitignored)

let destination = @"c:\git\alior-scrape"

let ac = new AliorClient(env "ALIOR_USERNAME", env "ALIOR_PASSWORD", isTest = true)

Directory.CreateDirectory destination |> ignore // Scrape moves the CSVs here and throws if the folder is missing
ac.Scrape(destination = destination, period = All)

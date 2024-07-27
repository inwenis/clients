#r "nuget: FSharp.Data, 6.4.0"
#r "nuget: PuppeteerSharp, 18.0.3"

#load "Utils.fs"
#load "AliorParsing.fs"

open System
open System.IO

open clients.AliorParsing

let home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
let downloads = Path.Combine(home, "Downloads")

let rows = Directory.EnumerateFiles(downloads, "Historia_Operacji_*.csv.CSV") |> parseFiles


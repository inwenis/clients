#load "C:/git/prelude/prelude.fsx"

#r "nuget: FSharp.Data, 6.4.0"
#r "nuget: PuppeteerSharp, 18.0.3"

#load "Utils.fs"
#load "AliorParsing.fs"

open System
open System.IO

open clients.AliorParsing


let rows = Directory.EnumerateFiles(@"c:\git\alior-scrape", "Historia_Operacji_*.csv.CSV") |> parseFiles

rows.Head
rows.Head.Transaction.TransactionDate

#load "C:/git/prelude/prelude.fsx"

#r "nuget: FSharp.Data, 6.6"
#r "nuget: PuppeteerSharp, 20.2.2"

#load "../src/Utils.fs"
#load "../src/AliorParsing.fs"

open System
open System.IO

open AliorParsing


let rows = Directory.EnumerateFiles(@"c:\git\alior-scrape", "Historia_Operacji_*.csv.CSV") |> parseFiles

rows.Head
rows.Head.Transaction.TransactionDate

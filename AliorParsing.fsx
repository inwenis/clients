#r "nuget: FSharp.Data, 6.4.0"
#r "nuget: PuppeteerSharp, 18.0.3"

#load "Utils.fs"

open System
open System.Text
open System.IO
open FSharp.Data

open clients.Utils

let home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
let downloads = Path.Combine(home, "Downloads")

type TransactionsAliorCsv = CsvProvider<
"""Data transakcji;Data księgowania;Nazwa nadawcy;Nazwa odbiorcy;Szczegóły transakcji;Kwota operacji;Waluta operacji;Kwota w walucie rachunku;Waluta rachunku;Numer rachunku nadawcy;Numer rachunku odbiorcy
31-07-2022;31-07-2022;;John Doe;Odsetki naliczone: 8.22 Pobrany podatek: 1.57 Odsetki skapitalizowane: 6.65;6,65;PLN;6,65;PLN;;91 1130 0007 0080 2394 3520 0002;
26-07-2022;26-07-2022;John Doe;Wow Polska S.A.;wow what a title;-42,42S;PLN;-42,42S;PLN;91 1130 0007 0080 2394 3520 0002;91 1130 0007 0080 2394 3520 0002;
26-07-2022;26-07-2022;John Doe;John Doe;Own (internal) transfer;-123,64;PLN;-123,64;PLN;91 1130 0007 0080 2394 3520 0002;91 1130 0007 0080 2394 3520 0002;
26-07-2022;26-07-2022;John Doe;John Doe;Own (internal) transfer;123,64;PLN;123,64;PLN;91 1130 0007 0080 2394 3520 0002;91 1130 0007 0080 2394 3520 0002;
""", ";", Quote='`'>

type AliorParsedRow = {
    File: string
    Data: TransactionsAliorCsv.Row
    LineNumber: int
    Product: string
}

let parseFile filePath =
    let lines = File.ReadAllLines(filePath, CodePagesEncodingProvider.Instance.GetEncoding(1250)) |> List.ofArray
    let header1 :: header2 :: rows = lines
    let product = extract "\d{26}" header1 // product aka. account number
    header2 :: rows
    |> String.concat "\n"
    |> TransactionsAliorCsv.Parse
    |> fun x -> x.Rows
    |> List.ofSeq
    |> List.mapi (fun i t -> { File = filePath; Data = t; LineNumber = i + 3; Product = product }) // +3 to align with line number in file

let allRows =
    Directory.EnumerateFiles(downloads, "Historia*.csv")
    |> List.ofSeq
    |> List.map (fun f -> parseFile f)
    |> List.collect id

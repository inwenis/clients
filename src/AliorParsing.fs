module AliorParsing

open System
open System.Text
open System.IO
open FSharp.Data
open Utils


type TransactionsAliorCsv = CsvProvider<
"""Data transakcji;Data księgowania;Nazwa nadawcy;Nazwa odbiorcy;Szczegóły transakcji;Kwota operacji;Waluta operacji;Kwota w walucie rachunku;Waluta rachunku;Numer rachunku nadawcy;Numer rachunku odbiorcy
31-07-2022;31-07-2022;;John Doe;Odsetki naliczone: 8.22 Pobrany podatek: 1.57 Odsetki skapitalizowane: 6.65;6,65;PLN;6,65;PLN;;91 1130 0007 0080 2394 3520 0002;
26-07-2022;26-07-2022;John Doe;Wow Polska S.A.;wow what a title;-42,42S;PLN;-42,42S;PLN;91 1130 0007 0080 2394 3520 0002;91 1130 0007 0080 2394 3520 0002;
26-07-2022;26-07-2022;John Doe;John Doe;Own (internal) transfer;-123,64;PLN;-123,64;PLN;91 1130 0007 0080 2394 3520 0002;91 1130 0007 0080 2394 3520 0002;
26-07-2022;26-07-2022;John Doe;John Doe;Own (internal) transfer;123,64;PLN;123,64;PLN;91 1130 0007 0080 2394 3520 0002;91 1130 0007 0080 2394 3520 0002;
""", ";", Quote='`'>


type TransactionAlior = {
    TransactionDate        : DateOnly
    AccountingDate         : DateOnly
    SenderName             : string
    ReceiverName           : string
    TransactionText        : string
    Amount                 : decimal
    TransactionCurrency    : string
    AmountInAccountCurrency: decimal
    AccountCurrency        : string
    SenderAccountNumber    : string
    ReceiverAccountNumber  : string
    // If there are identical transactions in a single file they will have different ordinal numbers.
    // The first transaction will have ordinal number 0, the second 1, etc. Ordinal numbers are assigned
    // in the order of appearance in the file.
    // OrdinalNumber is necessary for clients to avoid mistakenly removing transactions that appear to be duplicates.
    OrdinalNumber          : int
}

type AliorTransactionWithSourceFileInfo<'T> = {
    FullFileName  : string
    ScrapeDateTime: DateTimeOffset
    Product       : string
    Transaction   : 'T
    LineNumber    : int
}

let private parseFile fullFileName lines =
    let extractDateTime (fullFileName:string) =
        // sample file name - Historia_Operacji_2024-07-21_11-18-31.csv.CSV
        // somehow the extension is ".csv.CSV"
        let dateTime = regexExtract "\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}" fullFileName
        DateTimeOffset.ParseExact(dateTime, "yyyy-MM-dd_HH-mm-ss", null)

    let header1, header2, rows =
        match lines with
        | header1 :: header2 :: rows -> header1, header2, rows
        | _ -> failwithf "invalid file %s" fullFileName
    let product = regexExtract "\d{26}" header1 // product aka. account number
    header2 :: rows
    |> String.concat "\n"
    |> TransactionsAliorCsv.Parse
    |> fun x -> x.Rows
    |> List.ofSeq
    |> List.mapi (fun i t -> { FullFileName = fullFileName; ScrapeDateTime = extractDateTime fullFileName; Transaction = t; LineNumber = i + 3; Product = product }) // +3 to align with line number in file

let private parseFileAgain (transactions:AliorTransactionWithSourceFileInfo<TransactionsAliorCsv.Row> list) =
    transactions
    |> List.groupBy (fun a -> a.Transaction)
    |> List.map snd
    |> List.collect (fun group ->
        group
        |> List.sortBy (fun a -> a.LineNumber)
        |> List.mapi (fun i a ->
            let parsedAgain = {
                TransactionDate         = a.Transaction.``Data transakcji`` |> fun x -> DateOnly.ParseExact(x, "dd-MM-yyyy", null)
                AccountingDate          = a.Transaction.``Data księgowania`` |> fun x -> DateOnly.ParseExact(x, "dd-MM-yyyy", null)
                SenderName              = a.Transaction.``Nazwa nadawcy``
                ReceiverName            = a.Transaction.``Nazwa odbiorcy``
                TransactionText         = a.Transaction.``Szczegóły transakcji``
                Amount                  = a.Transaction.``Kwota operacji`` |> fun x -> decimal (x.Replace(",", "."))
                TransactionCurrency     = a.Transaction.``Waluta operacji``
                AmountInAccountCurrency = a.Transaction.``Kwota w walucie rachunku`` |> fun x -> decimal (x.Replace(",", "."))
                AccountCurrency         = a.Transaction.``Waluta rachunku``
                SenderAccountNumber     = a.Transaction.``Numer rachunku nadawcy``
                ReceiverAccountNumber   = a.Transaction.``Numer rachunku odbiorcy``
                OrdinalNumber           = i } : TransactionAlior
            {
                FullFileName   = a.FullFileName
                Transaction    = parsedAgain
                LineNumber     = a.LineNumber
                Product        = a.Product
                ScrapeDateTime = a.ScrapeDateTime
            }
        )
    )

let parseFiles files =
    files
    |> List.ofSeq
    |> List.map (fun f -> f, File.ReadAllLines(f, Encoding.UTF8) |> List.ofArray)
    |> List.filter (fun (_, lines) -> lines.Length > 0) // a file might be empty if the account has no transactions
    |> List.map (fun (f, lines) -> parseFile f lines)
    |> List.map (fun rows -> parseFileAgain rows)
    |> List.collect id

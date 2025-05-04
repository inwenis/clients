namespace clients

open System
open System.Text
open System.IO
open FSharp.Data
open clients.Utils


type TransactionsAliorCsv = CsvProvider<
"""Data transakcji;Data księgowania;Nazwa nadawcy;Nazwa odbiorcy;Szczegóły transakcji;Kwota operacji;Waluta operacji;Kwota w walucie rachunku;Waluta rachunku;Numer rachunku nadawcy;Numer rachunku odbiorcy
31-07-2022;31-07-2022;;John Doe;Odsetki naliczone: 8.22 Pobrany podatek: 1.57 Odsetki skapitalizowane: 6.65;6,65;PLN;6,65;PLN;;91 1130 0007 0080 2394 3520 0002;
26-07-2022;26-07-2022;John Doe;Wow Polska S.A.;wow what a title;-42,42S;PLN;-42,42S;PLN;91 1130 0007 0080 2394 3520 0002;91 1130 0007 0080 2394 3520 0002;
26-07-2022;26-07-2022;John Doe;John Doe;Own (internal) transfer;-123,64;PLN;-123,64;PLN;91 1130 0007 0080 2394 3520 0002;91 1130 0007 0080 2394 3520 0002;
26-07-2022;26-07-2022;John Doe;John Doe;Own (internal) transfer;123,64;PLN;123,64;PLN;91 1130 0007 0080 2394 3520 0002;91 1130 0007 0080 2394 3520 0002;
""", ";", Quote='`'>

module AliorParsing =

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

        let header1 :: header2 :: rows = lines
        let product = regexExtract "\d{26}" header1 // product aka. account number
        header2 :: rows
        |> String.concat "\n"
        |> TransactionsAliorCsv.Parse
        |> fun x -> x.Rows
        |> List.ofSeq
        |> List.mapi (fun i t -> { FullFileName = fullFileName; ScrapeDateTime = extractDateTime fullFileName; Transaction = t; LineNumber = i + 3; Product = product }) // +3 to align with line number in file

    let private parseAgain (transactions:AliorTransactionWithSourceFileInfo<TransactionsAliorCsv.Row> list) =
        transactions
        |> List.groupBy (fun x -> x.Transaction)
        |> List.map snd
        |> List.collect (fun x ->
            x |> List.mapi (fun i x ->
                let parsedAgain = {
                    TransactionDate =         x.Transaction.``Data transakcji`` |> fun x -> DateOnly.ParseExact(x, "dd-MM-yyyy", null)
                    AccountingDate =          x.Transaction.``Data księgowania`` |> fun x -> DateOnly.ParseExact(x, "dd-MM-yyyy", null)
                    SenderName =              x.Transaction.``Nazwa nadawcy``
                    ReceiverName =            x.Transaction.``Nazwa odbiorcy``
                    TransactionText =         x.Transaction.``Szczegóły transakcji``
                    Amount =                  x.Transaction.``Kwota operacji`` |> fun x -> decimal (x.Replace(",", "."))
                    TransactionCurrency =     x.Transaction.``Waluta operacji``
                    AmountInAccountCurrency = x.Transaction.``Kwota w walucie rachunku`` |> fun x -> decimal (x.Replace(",", "."))
                    AccountCurrency =         x.Transaction.``Waluta rachunku``
                    SenderAccountNumber =     x.Transaction.``Numer rachunku nadawcy``
                    ReceiverAccountNumber =   x.Transaction.``Numer rachunku odbiorcy``
                    OrdinalNumber =           i } : TransactionAlior
                {
                    FullFileName = x.FullFileName
                    Transaction = parsedAgain
                    LineNumber = x.LineNumber
                    Product = x.Product
                    ScrapeDateTime = x.ScrapeDateTime
                }
            )
        )

    let parseFiles files =
        files
        |> List.ofSeq
        |> List.map (fun f -> f, File.ReadAllLines(f, Encoding.UTF8) |> List.ofArray)
        |> List.filter (fun (_, lines) -> lines.Length > 0) // a file might be empty if the account has no transactions
        |> List.map (fun (f, lines) -> parseFile f lines)
        |> List.map (fun x -> parseAgain x)
        |> List.collect id

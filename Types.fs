namespace clients

open FSharp.Data

module Types =
    type Transfers = CsvProvider<"""FromAccount,ReceiverName,ReceiverAccount,TransferText,Amount,InsertedDateTime,Status,Type
Billing account name is long to align with head,Receiver1,   12 1234 1234 12,Title of tra, 58.00,2023-03-01T00:00:00.0000000+02:00,ToBeExecuted,Regular
Billing account name is long to align with head,Receiver3,   12 1234 1234 12,Title of tra, 12.52,2023-03-01T00:00:00.0000000+02:00,ToBeExecuted,Regular
Billing account name is long to align with head,Receiver2,   12 1234 1234 12,Title of tra, 27.95,2023-03-01T00:00:00.0000000+02:00,Executed,Tax
""">

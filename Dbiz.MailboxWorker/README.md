# DBiz Mailbox Worker (.NET 8 Worker Service)

This sample worker polls an Outlook mailbox using Microsoft Graph app-only authentication and logs new messages from the Inbox.

## What it does
- Connects to Microsoft Graph using **ClientSecretCredential**
- Reads messages from a configured mailbox and folder
- Filters to unread messages by default
- Keeps a simple local processed-state file so the same message is not handled twice
- Optionally marks messages as read after successful processing
- Leaves clear TODO hooks where you can create planner tasks in your own database

## Required Microsoft Graph setup
Use **application permissions** for a background service.

Minimum recommended Graph permission:
- `Mail.Read`

If you want the worker to mark processed mails as read, also add:
- `Mail.ReadWrite`

If you want to create or send drafts later, also add:
- `Mail.Send`

After adding permissions, your admin must grant **admin consent**.

## Required values from admin team
- Tenant ID
- App / Client ID
- Client Secret
- Mailbox address to monitor (user mailbox or shared mailbox)
- Confirmed Graph application permissions with admin consent

## Configure the app
Update `appsettings.json`:

```json
{
  "Graph": {
    "TenantId": "YOUR-TENANT-ID",
    "ClientId": "YOUR-APP-CLIENT-ID",
    "ClientSecret": "YOUR-APP-CLIENT-SECRET",
    "MailboxAddress": "planner@yourcompany.com",
    "MailFolderId": "Inbox"
  }
}
```

## Run locally
```bash
dotnet restore
dotnet run
```

## Install as Windows Service later
This project already includes `Microsoft.Extensions.Hosting.WindowsServices`.
You can publish it and register it as a Windows Service when you are ready.

## Main extension points
In `Services/MailboxReaderService.cs`, inside `HandleMessageAsync`, add your logic to:
1. store the email
2. extract JD / requirement details
3. create planner/task
4. apply rules and vendor assignment

## Notes
- The processed-state file is only for demo purposes. In production, keep processed message IDs or Graph delta tokens in your database.
- For large-scale mailbox sync, consider moving from simple polling to delta query / webhook strategies later.

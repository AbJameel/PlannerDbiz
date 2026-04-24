# DBiz Mailbox Worker (.NET 8 Worker Service)

This worker reads unread emails from Inbox using Microsoft Graph, downloads attachments, extracts JD text from supported attachments, and posts a planner-create payload to your own API.

## Flow
1. Read unread Inbox mails
2. Load the full message body
3. Download file attachments
4. Extract text from supported files (`.pdf`, `.docx`, `.txt`, `.csv`, `.html`)
5. Pick JD text from the best matching attachment, otherwise fall back to email body
6. POST a planner-create request to your planner API
7. Optionally mark the mail as read after successful processing

## Supported attachment text extraction
- PDF
- DOCX
- TXT / CSV / LOG / JSON / XML
- HTML / HTM

## Required Microsoft Graph permissions
Use application permissions for a worker/background service.

Minimum:
- `Mail.Read`

Also add this if you want to mark processed emails as read:
- `Mail.ReadWrite`

Admin consent is required.

## Config
Update `appsettings.json`.

### Graph section
- `MailboxAddress`: mailbox or shared mailbox to monitor
- `MailFolderId`: usually `Inbox`

### PlannerApi section
- `Enabled`: set `true` to call your planner API
- `BaseUrl`: planner API base URL
- `CreatePlannerPath`: relative endpoint path
- `ApiKey` or `BearerToken`: use whichever your planner API expects

## Payload sent to planner API
The worker sends a JSON payload containing:
- mail IDs
- subject
- from / to / cc
- received time
- body preview
- full body text
- extracted `JobDescriptionText`
- attachment metadata and extracted text

## Notes
- `JobDescriptionText` is selected first from attachments that look like JD/requirement files.
- If no suitable attachment text is found, the worker falls back to the email body text.
- For image-only PDFs or image attachments, OCR is not included in this sample.
- The processed-state JSON file is only a simple demo store. In production, keep processed IDs or delta tokens in your database.

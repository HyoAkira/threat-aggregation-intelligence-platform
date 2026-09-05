# phishtank-dataset-download Specification

## Purpose
Defines fetching of the PhishTank online-valid dataset from
the PhishTank data API and persistence of parsed entries into the
`providers.PhishTank` table with upsert semantics on `PhishId`.

## Requirements

### Requirement: PhishTank API service can download the full dataset

The system SHALL provide a service that can fetch the full PhishTank online-valid
dataset by downloading `online-valid.json.gz` from
`http://data.phishtank.com/data/online-valid.json.gz`, decompressing the gzip
stream, parsing the JSON array of phish entry objects, and upserting each entry
into `providers.PhishTank`.

#### Scenario: Full dataset download succeeds

- **WHEN** the service is invoked
- **THEN** the service downloads the gzip-compressed JSON file
- **AND** the gzip stream is decompressed
- **AND** the JSON array is parsed into phish records
- **AND** each record is upserted into `providers.PhishTank` (insert or update on
  conflict for `PhishId`)
- **AND** the number of records processed is returned

#### Scenario: API returns HTTP error

- **WHEN** the PhishTank API returns a non-success status code
- **THEN** an `HttpRequestException` is thrown containing the status code and error
  body

#### Scenario: Gzip stream does not contain valid JSON

- **WHEN** the decompressed response body is not a valid JSON array
- **THEN** an `InvalidDataException` is thrown

#### Scenario: API returns empty or malformed JSON

- **WHEN** the parsed JSON is not an array or is empty
- **THEN** an exception is thrown

### Requirement: PhishTank phish entries are upserted on conflict

The system SHALL upsert phish entries into `providers.PhishTank` using the unique
constraint on `PhishId`. When a record with the same `PhishId` already exists, all
other columns are updated; when no matching record exists, a new row is inserted.

#### Scenario: New phish entry is inserted

- **WHEN** a phish entry with a new `PhishId` is fetched
- **THEN** a new row is inserted into `providers.PhishTank`

#### Scenario: Existing phish entry is updated

- **WHEN** a phish entry with the same `PhishId` as an existing row is fetched
- **THEN** the existing row is updated with the new column values
- **AND** no duplicate row is created

### Requirement: PhishTank JSON fields map to PhishTank entity columns

The PhishTank API JSON response is a **flat JSON array** of phish entry objects.
The system SHALL map the following JSON fields to the `PhishTank` entity columns,
converting string-based boolean values (`"yes"`/`"no"`) to `bit`:

| JSON Field | Entity Column | Conversion |
|---|---|---|
| `phish_id` | `PhishId` (int) | Direct integer |
| `url` | `Url` (nvarchar(max)) | Direct string |
| `phish_detail_url` | `PhishDetailUrl` (nvarchar(max)) | Direct string |
| `submission_time` | `SubmissionTime` (datetime2) | ISO 8601 parse |
| `verified` | `Verified` (bit) | `"yes"`→true, `"no"`→false |
| `verification_time` | `VerificationTime` (datetime2, nullable) | ISO 8601 parse, null if empty |
| `online` | `Online` (bit) | `"yes"`→true, `"no"`→false |
| `target` | `Target` (nvarchar(500), nullable) | Direct string |
| `details` | `Details` (nvarchar(max), nullable) | Serialize inner JSON array as string |

#### Scenario: JSON fields are correctly mapped

- **WHEN** a JSON object with `phish_id`, `url`, `phish_detail_url`,
  `submission_time`, `verified`, `verification_time`, `online`, `target`, and
  `details` fields is parsed
- **THEN** each field is mapped to the corresponding entity property
- **AND** `verified` and `online` string values (`"yes"`/`"no"`) are converted to
  `bit`
- **AND** the `details` nested array is serialized as a JSON string in the
  `Details` column

### Requirement: PhishTank API fetching is triggered manually via HTTP endpoint

The system SHALL provide an HTTP endpoint that allows a user to manually trigger
PhishTank entry fetching.

#### Scenario: Full dataset download endpoint

- **WHEN** `GET /api/phishtank/download` is called
- **THEN** the service fetches the full dataset
- **AND** the response contains the number of phish entries processed

#### Scenario: Upstream API error returns 502

- **WHEN** the PhishTank data API returns a non-success status code
- **THEN** an HTTP 502 response is returned with the error body

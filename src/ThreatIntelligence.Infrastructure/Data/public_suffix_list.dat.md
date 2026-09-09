# Public Suffix List snapshot

| Field          | Value                                                                                              |
| -------------- | -------------------------------------------------------------------------------------------------- |
| Source URL     | https://publicsuffix.org/list/public_suffix_list.dat                                               |
| Retrieval date | 2026-09-09                                                                                         |
| Upstream build | 2026-09-08_12-18-37_UTC                                                                            |
| Upstream commit| 3955e3ec29b94c3cca7bd4509c5f14a7c0959e26                                                           |
| SHA-256        | `4b673689999dbaca60b93fa3e1da5752505ef9717b1c4dc44acbfdafd35679ea`                                |

## Update procedure

Replacement is an intentional, reviewed maintenance change:

1. Download a fresh copy from the Source URL.
2. Replace `public_suffix_list.dat` and update this file (retrieval date, upstream hash, SHA-256).
3. Run the full PSL regression test suite against the updated snapshot.

The application never downloads this file at startup and performs no runtime refresh.
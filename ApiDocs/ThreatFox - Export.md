---
title: "ThreatFox - Export"
source: "https://threatfox.abuse.ch/export/"
author:
published:
created: 2026-09-04
description: "Export indicators of compromise (IOCs) from ThreatFox"
tags:
  - "clippings"
---
## Export IOCs

ThreatFox offers the exporting of indicators of compromise (IOCs) in following formats:

[Auth-Key (**Required**)](#auth_key) [Daily MISP Events](#misp) [Suricata IDS Ruleset](#suricata) [DNS Response Policy Zone (RPZ)](#rpz) [host file (domain only)](#hostfile) [JSON file](#json) [CSV files](#csv)

> [!info] Expiration of IOCs
> Since 2025-05-01, we are expiring IOCs that are older than 6 months. We are doing so to avoid false positives, mostly happening on cloud based infrastructure where assets (e.g. IP addresses) are changing customers quickly. Expired IOCs are not exposed on the ThreatFox API and data Export. However, they are still visibile and searchable through the ThreatFox UI, but flagged to be expired.

## Obtain an Auth-Key (Required)

---

In order to access the datasets listed below, you need to obtain an `Auth-Key` first. If you don't have one you can get one for free here:

- [abuse.ch Authentication Portal](https://auth.abuse.ch/)

Whenever you try to download a dataset or file from below, you must include your `Auth-Key` in the URL. Example curl command:

```
curl -i "https://threatfox-api.abuse.ch/v2/files/exports/YOUR-AUTH-KEY-HERE/full.csv.zip"
```

## Daily MISP Events

---

If you use [MISP](https://www.misp-project.org/ "Malware Information Sharing Platform (MISP)") you can ingest URLhaus as a daily feed. To do so, you simply have to click on the button below to copy your personal MISP Feed URL to your clipboard. Afterwards, navigate to `Sync Actions -> Feeds` and update or create the URLhaus feed with your personal MISP Feed URL from your clipboard.

When using the ThreatFox MISP event feed, we recommend you to remove the `IOC flag` for IOCs on our events that older than 6 month to avoid false positives.

Copy MISP Feed URL

## host file (domains only)

---

Some commercial and open source security software (such as [Pi-hole](https://pi-hole.net/ "pi-hole")) can detect access to domain names based on the `host file` format. For this purpose, ThreatFox offers a list of domain based IOCs. The host file below contains the following datasets observed in the **past 6 month**:

- Payload delivery domains
- Botnet C2 domains

The following file gets generated every **5 minutes**. To achieve the best protection, we recommend to fetch it every 5 minutes.

## Suricata IDS Ruleset

---

ThreatFox provides a ruleset containing all network based Indicators Of Compromise (IOCs) for [Suricata IDS](https://suricata-ids.org/ "Suricata IDS"). As we believe that IOCs have an expiration date too and to avoid false positive, we only export IOCs for the **past 6 month**. Please note that the ruleset has been tested with Suricata version 6.0.0. The ruleset gets generated **every 5 minutes**. To achieve the best protection, we recommend to fetch it every 5 minutes.

## DNS Response Policy Zone (RPZ)

---

By using an DNS Reponse Policy Zone (RPZ), also known as DNS firewall, you can detect the resolution of certain domain names ovserved in the **past 6 month** on your DNS resolver. ThreatFox offerst the following IOCs as RPZ dataset:

- Payload delivery domains
- Botnet C2 domains

More information about DNS RPZ can be found on [dnsrpz.info](https://dnsrpz.info/ "DNS Response Policy Zones"). The following file gets generated every **5 minutes**. To achieve the best protection, we recommend to fetch it every 5 minutes.

## JSON files

---

The following data exports exists in JSON format:

- Recent additions ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/recent.json "Recent additions"))
- Full data dump ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/full.json.zip "Full data dump") - zip compressed)
- URLs: Recent additions ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/urls_recent.json "URLs: Recent additions"))
- URLs: Full data dump ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/urls_full.json.zip "URLs: Full data dump") - zip compressed)
- Domains: Recent additions ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/domains_recent.json "Domains: Recent additions"))
- Domains: Full data dump ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/domains_full.json.zip "Domains: Full data dump") - zip compressed)
- IP-port: Recent additions ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/ip_port_recent.json "IP-port: Recent additions"))
- IP-port: Full data dump ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/ip_port_full.json.zip "IP-port: Full data dump") - zip compressed)
- MD5 hashes: Recent additions ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/md5_recent.json "MD5 hashes: Recent additions"))
- MD5 hashes: Full data dump ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/md5_full.json.zip "SMD5 hashes: Full data dump") - zip compressed)
- SHA256 hashes: Recent additions ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/sha256_recent.json "SHA256 hashes: Recent additions"))
- SHA256 hashes: Full data dump ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/sha256_full.json.zip "SHA256 hashes: Full data dump") - zip compressed)

> [!info] Note
> **Recent** datasets ("recent additions") include IOCs for the last 48 hours (`first_seen` or `last_seen` within the past 48 hours) and are being generated every **5 minutes**. Please do not fetch them more often than that.  
> **Full** data dumps include all IOCs and are only being generated **once per hour**. Please do not fetch them more often than once per hour.

## CSV files

---

The following data exports exists in CSV format:

- Recent additions ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/recent.csv "Recent additions"))
- Full data dump ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/full.csv.zip "Full data dump") - zip compressed)
- URLs: Recent additions ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/urls_recent.csv "URLs: Recent additions"))
- URLs: Full data dump ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/urls_full.csv.zip "URLs: Full data dump") - zip compressed)
- Domains: Recent additions ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/domains_recent.csv "Domains: Recent additions"))
- Domains: Full data dump ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/domains_full.csv.zip "Domains: Full data dump") - zip compressed)
- IP-port: Recent additions ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/ip_port_recent.csv "IP-port: Recent additions"))
- IP-port: Full data dump ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/ip_port_full.csv.zip "IP-port: Full data dump") - zip compressed)
- MD5 hashes: Recent additions ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/md5_recent.csv "MD5 hashes: Recent additions"))
- MD5 hashes: Full data dump ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/md5_full.csv.zip "SMD5 hashes: Full data dump") - zip compressed)
- SHA256 hashes: Recent additions ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/sha256_recent.csv "SHA256 hashes: Recent additions"))
- SHA256 hashes: Full data dump ( [download](https://threatfox-api.abuse.ch/v2/files/exports/a9dd3775075108c74acfa49393c2bb0bfbd1aa52c7c5ae23/sha256_full.csv.zip "SHA256 hashes: Full data dump") - zip compressed)

> [!info] Note
> **Recent** datasets ("recent additions") include IOCs for the last 48 hours (`first_seen` or `last_seen` within the past 48 hours) and are being generated every **5 minutes**. Please do not fetch them more often than that.  
> **Full** data dumps include all IOCs and are only being generated **once per hour**. Please do not fetch them more often than once per hour.
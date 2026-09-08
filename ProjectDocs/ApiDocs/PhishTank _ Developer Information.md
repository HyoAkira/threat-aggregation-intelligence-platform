---
title: "PhishTank > Developer Information"
source: "https://phishtank.com/developer_info.php"
author:
published:
created: 2026-09-05
description:
tags:
  - "clippings"
---
## Developer Information

### User Agent String

We require that you use a descriptive User Agent string in your application to identify the application. If your User Agent is blank or generic, you may recieve an increased number of rate limited requests or be redirected to additional security checks. The recommended format is:

**Header Parameters**

| Name | Value |
| --- | --- |
| User-Agent | Descriptive user agent string, e.g. ``` phishtank/[username] ``` |

### Get the Database

If you'll be doing lots of lookups, the best option is to take advantage of our downloadable databases. Available in multiple formats and updated hourly, these make it easy to have fast and up to date phishing detection built into your application.

The data is available in a variety of formats to make it as easy as possible for you to implement. We're always open to suggestions for additional formats we could provide, so if you have any thoughts, please join our [developers list](https://phishtank.com/mailing_lists.php) and let us know!

If you do intend to fetch these files automatically, please [register for an application key](https://phishtank.com/api_register.php) and see below for instructions on how to use it to request files. Without this key, you will be limited to a few downloads per day.

### Format Options

| XML |
| --- |
| http://data.phishtank.com/data/online-valid.xml |
| http://data.phishtank.com/data/online-valid.xml.gz |
| http://data.phishtank.com/data/online-valid.xml.bz2 |

| CSV |
| --- |
| http://data.phishtank.com/data/online-valid.csv |
| http://data.phishtank.com/data/online-valid.csv.gz |
| http://data.phishtank.com/data/online-valid.csv.bz2 |

| Serialized PHP |
| --- |
| http://data.phishtank.com/data/online-valid.php\_serialized |
| http://data.phishtank.com/data/online-valid.php\_serialized.gz |
| http://data.phishtank.com/data/online-valid.php\_serialized.bz2 |

| JSON |
| --- |
| http://data.phishtank.com/data/online-valid.json |
| http://data.phishtank.com/data/online-valid.json.gz |
| http://data.phishtank.com/data/online-valid.json.bz2 |

### Formats Definitions

**XML**
```
<output>
    <meta>
        <total_entries>1234</total_entries>
    </meta>
    <entries>
        <entry>
            <url><![CDATA[https://www.example.com/]]></url>
            <phish_id>123456</phish_id>
            <phish_detail_url>http://www.phishtank.com/phish_detail.php?phish_id=123456</phish_detail_url>
            <details>
                <detail>
                    <ip_address>1.2.3.4</ip_address>
                    <cidr_block>1.2.3.0/24</ip_address>
                    <announcing_network>1234</announcing_network>
                    <rir>arin</rir>
                    <detail_time>2009-06-20T15:37:31+00:00</detail_time>
                </detail>
            </details>
            <submission>
                <submission_time>2009-06-19T15:15:47+00:00</submission_time>
            </submission>
            <verification>
                <verified>yes</verified>
                <verification_time>2009-06-19T15:37:31+00:00</verification_time>
            </verification>
            <status>
                <online>yes</online>
            </status>
            <target>1st National Example Bank</target>
        </entry>
        ...
    </entries>
</output>
```
**CSV**
```
phish_id,url,phish_detail_url,submission_time,verified,verification_time,online,target
123456,https://www.example.com/,http://www.phishtank.com/phish_detail.php?phish_id=123456,2009-06-19T15:15:47+00:00,yes,2009-06-19T15:37:31+00:00,yes,1st National Example Bank
```
**PHP/JSON**
```
array(
      array(
            'phish_id' => 123456,
            'url' => 'https://www.example.com/',
            'phish_detail_url' => 'http://www.phishtank.com/phish_detail.php?phish_id=123456',
            'submission_time' => '2009-06-19T15:15:47+00:00',
            'verified' => 'yes',
            'verification_time' => '2009-06-19T15:37:31+00:00',
            'online' => 'yes',
            'target' => '1st National Example Bank',
            'details' => array(
                               array(
                                     'ip_address' => '1.2.3.4',
                                     'cidr_block' => '1.2.3.0/24',
                                     'announcing_network' => '1234',
                                     'rir' => 'arin',
                                     'detail_time' => '2006-10-01T02:30:54+00:00'
                                    )
                               )
           )
)
```

### Column Definitions

**phish\_id** The ID number by which Phishtank refers to a phish submission. All data in PhishTank is tied to this ID. This will always be a positive integer.

**phish\_detail\_url** PhishTank detail url for the phish, where you can view data about the phish, including a screenshot and the community votes.

**url** The phish URL. This is always a string, and in the XML feeds may be a CDATA block.

**submission\_time** The date and time at which this phish was reported to Phishtank. This is an ISO 8601 formatted date.

**verified** Whether or not this phish has been verified by our community. In these data files, this will always be the string 'yes' since we only supply verified phishes in these files.

**verification\_time** The date and time at which the phish was verified as valid by our community. This is an ISO 8601 formatted date.

**online** Whether or not the phish is online and operational. In these data files, this will always be the string 'yes' since we only supply online phishes in these files.

**target** The name of the company or brand the phish is impersonating, if it's known.

### Using your application key

Once you have registered for an application key, make your request as you normally would, but format the URL this way:
```
http://data.phishtank.com/data/<your app key>/online-valid.json.bz2
```

### Checking for updates

If you're passing an application key with your request, you may make unlimited HTTP HEAD requests to check the ETag header. These requests won't count against any rate limiting. Note that the files are still only updated every hour on the hour, so there's no need to check every minute.
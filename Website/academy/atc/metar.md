# METARs
---  

## What is a METAR  
An Aviation Routine Weather Report (METAR) is an observation of current surface weather reported in a standard international format. METARs are issued hourly unless significant weather changes have occurred. A special METAR (SPECI) can be issued at any interval between routine METAR reports.  

## How to Read a METAR  
Here's what a typical METAR looks like:

```
METAR KGGG 1617753Z AUTO 14021G26 3/4SM+ TSRA BR BKN008 OVC012CB 18/17 A2970 RMK PRESFR 
```

Whoa, this looks like someone fell asleep on their keyboard or something. I can assure you though, that's not the case with this report. Let's take a closer look.
![METAR KGGG](https://xa.ivao.aero/backend/upload/files/62bf20aa1bfb5_How-to-Read-a-METAR.webp)

So a typical METAR report contains the following information in sequential order:
1. **Type of report**&mdash;there are two types of METAR reports. The first is the routine METAR report that is transmitted every hour. The second is a special report, a SPECI, that can be given at any time to update the METAR for rapidly changing weather conditions, aircraft mishaps, or other critical information. So here, it'll say either `METAR` or `SPECI`.
1. **Station identifier**&mdash;a four-letter code as established by the International Civil Aviation Organization (ICAO). In the 48 contiguous states, a unique three-letter identifier is preceded by the letter `K`. For example, Gregg County Airport in Longview, Texas, is identified by the letters `KGGG`, `K` being the country designation and `GGG` being the airport identifier. In other regions of the world, including Alaska and Hawaii, the first two letters of the four-letter ICAO identifier indicate the region, country, or state. Alaska identifiers always begin with the letters `PA`, and Hawaii identifiers always begin with the letters `PH`.
1. **Date and time of report**&mdash;depicted in a six-digit group (`161753Z`). The first two digits are the date, the 16th of the month, and the last four digits are the time of the METAR, which is always given in coordinated universal time (UTC), otherwise known as Zulu time. A `Z` is appended to the end of the time to denote that the time is given in Zulu time (UTC) as opposed to local time. 
1. **Modifier**&mdash;denotes that the METAR came from an automated source or that the report was corrected. If the notation `AUTO` is listed in the METAR, the report came from an automated source. It also lists `AO1` or `AO2` in the remarks section to indicate the type of precipitation sensors employed at the automated station. When the modifier `COR` is used, it identifies a corrected report sent out to replace an earlier report that contained an error (for example: `METAR KGGG 161753Z COR`).
1. **Wind**&mdash;reported with five numbers (`14021`), unless the speed is greater than 99 knots, in which case the wind is reported with six numbers. The first three numbers indicate the direction from which the wind is blowing to the nearest ten degrees in relation to TRUE North. If the wind is variable, then `VRB` will go after the numbers. The last two digits indicate the speed of the wind in knots, unless the wind is greater than 99 knots, in which case it is indicated by three digits. If the winds are gusting, the letter `G` follows the wind speed numbers, and then the numbers right after G indicate the highest expected wind gusts (e.g. `G26`).
1. **Visibility**&mdash;the prevailing visibility (`3/4 SM`) is reported in statute miles as denoted by the letters `SM`. It is reported in both miles and fractions of miles. In this case, 3/4 of a mile.
1. **Weather**&mdash;there are three parts to the weather section. The first is a qualifier of intensity. The intensity may be light (`-`), moderate ( ), or heavy (`+`). Because we're seeing a `+` symbol, that indicates heavy. Then, if there's any kind of weather phenomena that's in the immediate vicinity of the airport, that'll be shown. So in this example, `TS` stands for thunderstorm, and `RA` stands for rain. If you happen to see a notation of `VC` in this section, that indicates a specific weather phenomenon is in the vicinity of five to ten miles from the airport. And then finally, the third part of this weather section are the descriptors, which are used to describe certain types of precipitation and obscurations. So here we're seeing `BR`, which stands for mist. You might also see things like `RA`, which stands for rain, `HZ`, which stands for haze, or `SN`, which stands for snow.
1. **Sky condition**&mdash;here we're getting a sense of what cloud cover looks like. In this first part, we're seeing the height of the cloud base, which is being reported with a three-digit number in hundreds of feet above-ground-level (AGL). `BKN` stands for broken clouds, and `008` means 800 feet. So the cloud base is at 800 feet AGL. That's one thing that's happening. Then we're also seeing `OVC`, which stands for overcast, and the `012` stands for 1,200 ft. So we have overcast clouds at 1,200 ft. AGL. And we're seeing the type of cloud here too. `CB` stands for cumulonimbus clouds, and another indicator you might see here is `TCU`, which stands for towering cumulus clouds.
1. **Temperature and dew point**&mdash;the air temperature and dew point are always given in whole degrees Celsius (&deg;C) and separated by a forward slash (`/`). Temperatures below 0&deg;C are preceded by the letter `M` to indicate minus.
1. **Altimeter setting**&mdash;`A2970` means that a manned aircraft pilot using an altimeter would set his or her altimeter pressure to 29.70 Hg, or inches of mercury.
1. **Remarks**&mdash;the remarks section always begins with the letters `RMK`. Comments may or may not appear in this section of the METAR. The information contained in this section may include wind data, variable visibility, beginning and ending times of particular phenomenon, pressure information, and various other information deemed necessary. In the above example, `PRESFR` means "pressure falling rapidly." Another example of a remark regarding weather phenomenon that does not fit in any other category would be: `OCNL LTGICCG`. This translates as occasional lightning in the clouds and from cloud to ground. Automated stations also use the remarks section to indicate the equipment needs maintenance.

## METAR Example
Let's run through another example, section by section:
![METAR KBNA](https://xa.ivao.aero/backend/upload/files/62bf21f921b9e_METAR-Example.webp)

1. `METAR` is a routine weather observation
1. `KBNA` is Nashville International Airport, Nashville, TN
1. `281251Z` means the observation was taken on the 28th of the month at 1251 UTC (or Z)
1. `AUTO` means the report came from an automated source
1. `12008KT` means the wind is from 120&deg; true at 8 knots
1. `4SM` means the visibility is 4 statute miles
1. `-RA HZ` means light rain and haze
1. `BKN010 OVC023` means ceiling 1,000 ft. broken, 2,300 ft. overcast
1. `21/17` means the temperature is 21&deg;C and the dew point is 17&deg;C
1. `A3005` means the altimeter setting is 30.05 in. of Hg
1. `RMK RAB25` means remarks: rain began at 25 min. past the hour (1225 UTC)
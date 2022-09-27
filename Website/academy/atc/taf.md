# TAFs
---  

## What is a TAF
A Terminal Aerodrome Forecast (TAF) is a point meteorological condition forecast covering expected weather for a 24 or 30 hour timeframe within a 5 statute mile radius of the airport for which the TAF is being issued. Beyond that radius, pilots must consult their area forecasts.

## The Three Types of TAF
As a pilot, you may encounter three different types of TAF reports. The first type is the most common with the other two types being issued as needed depending on circumstances.

1. **Routine Forecast (`TAF`)**  
    The regularly scheduled routine forecast that is released at the standard issue time is referred to simply as a `TAF`. Routine TAFs are issued four times a day at a frequency of every six hours. Pilots will receive new TAFs at 0000, 0600, 1200, and 1800 Zulu time each day. Most TAFs are generated for a time period of the upcoming 24 hours and in some locations TAFs can be issued for predicted weather up to 30 hours in advance.

1. **Amended forecast (`TAF AMD`)**  
    If a change or correction needs to be made to the previously issued standard TAF, the update will be coded as `TAF AMD`. The `AMD` signifies that the original TAF has been amended and that the new TAF AMD supersedes the original. Amended TAFs are issued whenever the currently active TAF no longer accurately represents the expected weather conditions.

    While routine forecast TAFs are issued for a time period of 24 or 30 hours, an amended TAF may have a much shorter forecast period. A TAF that is issued at a nonstandard time is an amended forecast.

1. **Corrected (`COR`) or Delayed (`RTD`)**  
    If the TAF being issued has been corrected for an error or was delayed in being issued, this will be indicated by `COR` or `RTD`. Look for these abbreviations in the communications header preceding the text of the TAF.

    Delayed TAFs are most common at locations that operate on a part-time basis. When the observer returns, they complete two observation cycles prior to issuing a TAF. The first TAF issued during their shift is coded as delayed (`RTD`).

## Five Parts of a TAF
The TAF is made up of five parts, always presented in the same order and written using specific codes and abbreviations. The first four parts give the basic information about what the report is, where it is for, when it was created, and the time frame it covers. The fifth part makes up the bulk of the TAF and includes the meteorological condition predictions.
![TAF KPDX](https://xa.ivao.aero/backend/upload/files/62c5be8a007bd_TAF.PNG)

### ICAO Station Identifier
Immediately following the report type is the ICAO station identifier. This is the four-letter code that uniquely identifies each location. The identifier starts with the ICAO international prefix and is followed by the domestic identifier.

Stations within the continental United States have a 3-letter domestic identifier preceded by a `K` prefix. Alaska's 2-letter domestic identifier follows a `PA` prefix, and Hawaii uses a `PH` prefix, with the `P` indicating their location in the Pacific and the `H` standing for Hawaii. If you will be controlling internationally, learn the ICAO prefixes for your locations. The first one or two letters of international identifiers indicate the region of the world and the country or state of the location. For example, stations in Canada have a `CU`, `CW`, `CY`, or `CZ` prefix, and Mexico uses `MM`.

### Date and Time of Origin
The date and time following the ICAO station identifier indicate when the forecast included in the TAF was prepared. The date is formatted using two digits for the day of the month. Due to the short-term nature of TAFs, the month and year are not part of the date code.

After the date is a four-digit time showing hours and minutes. The two digits for hour are given in 24-hour format from 00 to 23 and are followed by two digits for minutes. A "Z" follows the date and time grouping to remind pilots that like other aviation times, the time listed in the TAF is in UTC, or "Zulu" time, not local time.

The time of origin, or the time the TAF is published, will usually be within an hour of the start of the valid period time since forecasters want to give pilots the most current forecast possible.

### Valid Period Date and Time
The valid period date and time informs pilots of the time range that the information in the TAF applies to. The date and time are formatted using two sets of four digits each with the first two digits representing the day of the month and the second two the 24-hour time in hours only (no minutes since TAFS are issued on the hour). The first set of digits is the start date/time and the second is the end date/time.

Remember that TAFs are published every 6 hours and are valid for either 24 or 30 hours. This means that part of the valid time for a new TAF will overlap with the valid time for an older TAF, but the newer TAF takes precedence because it replaces the older TAF upon issue.

Some locations do not offer full-time reporting services. In this case, the abbreviation `AMD NOT SKED` will be added to the forecast to indicate that there will be no subsequent amendments even if conditions change because no forecaster is on duty to generate them. On a digital TAF, expanding the `AMD NOT SKED` notification will provide additional information such as the observation ending time (`AFT` DDHHmm), scheduled time that observations will resume (`TIL` DDHHmm), and the period of observation unavailability (DDHH`/`DDHH).

### Forecast Meteorological Conditions
The fifth section of the TAF contains the details of the weather forecast. The forecast details are broken down into 5 categories: wind, visibility, weather, sky condition, and optional data (wind shear).

The forecast meteorological conditions portion of the TAF starts out with the initial forecast conditions. Wind, visibility, and sky condition are always included in the initial forecast. Weather and optional data like wind shear are only included in the initial forecast if they are a factor.

#### Wind
Wind information is for the predicted direction and speed of surface winds. Both of these data points are combined into one block of text. The first three digits relay the wind direction in tens of degrees from true north. If winds are predicted to be sustained, the remaining two or three digits are the wind speed in knots, and this is denoted by a `KT` following the wind speed. Thus, wind information of `12008KT` means that the wind is predicted to come from one-two-zero degrees and reach sustained speeds of 8 knots.

When wind gusts are expected, the wind direction and sustained speed will be given, followed by `G` for gusts, the wind speed of the highest predicted gusts, and the standard `KT` for knots. For example, `26013G21KT` means that the wind direction is two-six-zero degrees with sustained winds at 13 knots and gusts expected up to 21 knots.

If wind speeds of under three knots are predicted, the wind data will read `00000KT` meaning that it is calm and there is no significant wind from any direction. This is read as "wind calm".

Variable winds that are not sustained from any one direction have `VRB` at the beginning rather than a three-digit wind direction code.

#### Visibility
Visibility is measured in statute miles down to a quarter mile. If visibility is in even mile increments, the format will be the number of miles immediately followed by `SM` for statute mile. Fractions of a mile are written following the whole mile number with a space between the two. For example, `3SM` is a 3-statute mile visibility. `3 1/2SM` is a 3 and one-half statue mile visibility. If visibility is over six statute miles, the visibility information will read `P6SM`.

#### Weather
Weather phenomena are coded using the same format, qualifiers, and contractions as a METAR. Qualifiers include the intensity or proximity of the phenomenon, a qualifier descriptor, the type of precipitation or obscuration, and other information as relevant.

According to the NOAA Aviation Weather Center, the qualifier codes that you may see are:

#### Qualifiers of Intensity or Proximity
- `-` Light
- Moderate (no qualifier)
- `+` Heavy or well-developed
- `VC` in the Vicinity

#### Qualifier Descriptor

- `MI` Shallow
- `BC` Patches
- `DR` Low Drifting
- `BL` Blowing
- `SH` Showers
- `TS` Thunderstorm
- `FZ` Freezing
- `PR` Partial

#### Precipitation

- `DZ` Drizzle
- `RA` Rain
- `SN` Snow
- `SG` Snow Grains
- `IC` Ice Crystals
- `PL` Ice Pellets
- `GR` Hail
- `GS` Small Hail or Snow Pellets (less than 1/4 inch in diameter)
- `UP` Unknown precipitation (automated stations only)

#### Obscuration

- `BR` Mist (Foggy conditions with visibilities greater than 5/8 statute mile)
- `FG` Fog (visibility 5/8 statute mile or less)
- `FU` Smoke
- `DU` Dust
- `SA` Sand
- `HZ` Haze
- `PY` Spray
- `VA` Volcanic Ash

#### Other

- `PO` Well-Developed Dust/Sand Whirls
- `SQ` Squalls
- `FC` Funnel Cloud
- `+FC` Well-Developed Funnel Cloud, Tornado or Waterspout
- `SS` Sandstorm
- `DS` Duststorm

The code `NSW` or "No Significant Weather" is used to denote a period of no significant weather following a period where significant weather was predicted.

### Sky Condition

The sky condition segment of the report communicates the presence and distribution of clouds along with the visibility. Unlike METARs, TAFs only identify cumulonimbus clouds (`CB`) as they pose the threat of a developing thunderstorm. The sky condition portion of the TAF will list the sky condition, for example `SKC` meaning sky clear. If the report anticipates clouds, it will describe their distribution (e.g. `SCT` for scattered) and altitude (using 3 digits in hundreds of feet where `001` would be 100 feet). Only the lowest ceiling layer is annotated and described in the TAF. If surface level phenomenon are anticipated, the sky condition will read `VV` followed by 3 digits predicting the vertical visibility (VV) into the obscuration in hundreds of feet.

### Optional Data (Wind Shear)

If wind shear is expected, it will be noted immediately after the sky condition portion of the forecast. The format for wind shear is a `WS` for wind shear followed by the 3-digit height of the shear in hundreds of feet AGL up to and including 2,000 feet. A forward slash separates the height from the predicted wind direction and speed at the shear height. `KT` for knots is added to the end of the wind shear data block. As an example, `WS015/21040KT` means that wind shear is expected at 1,500 feet AGL. The wind will be coming from two-one-zero degrees at a speed of 40 knots.

## TAF Meteorological Conditions Change Terminology

The initial forecast is the beginning of the weather reporting, however as a TAF covers either a 24 or a 30-hour period, conditions can be expected to change during that time frame. When significant change in conditions is expected during the lifespan of the TAF, additional time periods and meteorological conditions information will be added below the initial forecast.

### `FM`

`FM` stands for "From". It is used when conditions are expected to change rapidly over a short time frame - usually less than one hour. This is common when a storm front moves through. You will see `FM` followed by a four-digit number indicating the hour and minute the transition is expected to begin. You will often see an `FM` group on one line of the TAF followed by another `FM` group on the next line. This means that conditions are expected to change per the first `FM` and stay at the new state until the next `FM`.

### `TEMPO`

If conditions are notable, but are expected to last for less than an hour at any given time and occur during a total of less than half of the time period of the TAF, a `TEMPO` group, or temporary code can be used. Only the conditions that are changing are listed on a TEMPO line. Any condition not listed on this line should be expected to remain as it was during the previous time group.

### `PROB`

`PROB` stands for probability forecast. It is used to estimate the likelihood of a weather event like precipitation or thunderstorm occurring during the given time. `PROB` is paired with a number representing a probability percentage range. For example, `PROB40` is used to communicate odds greater than 30% and less than 50%.  

Following `PROB`, you will see a four-digit number giving the time range for the prediction. The first two numbers are the 24-hour beginning hour of the range and the last two numbers indicate the ending time. Following the time is a description of visibility and what type of conditions to expect.

### `BECMG`

Like `FM`, `BECMG` for becoming indicates a change in conditions. In this case, the conditions are changing more slowly over the course of a longer period of time as is the case when cloud cover slowly dissipates over a two-hour time frame.

The BECMG line starts by carrying over the changing condition from the previous line. If it was overcast (`OVC`), that the `BECMG` line will start with `OVC` and the altitude. It will then say `BECMG` and list the time frame of the transition using four-digits with the first two being the beginning hour and the last two being the ending hour of the transition. Following the time is a description of what the conditions will be following the transition (e.g. `BKN` for broken clouds).

## Difference Between TAFs and METARs

Since TAFs and METARs both involve weather data, it can be easy to get the two confused. The important distinction between them is that a TAF is just a forecast. It predicts future weather. METARs, on the other hand, are issued to advice pilots of current real-time weather conditions. METARs communicate actual weather, not predictions. Both TAF and METAR reports do however use the same weather codes and similar formatting, so once you learn how to read one, the other should be no problem.

Remember that while METARs indicate many cloud types, TAFs only report the expected presence of cumulonimbus clouds since they are the ones that accompany thunderstorms. A METAR also communicates multiple cloud layers, while a TAF indicates only the lowest ceiling.
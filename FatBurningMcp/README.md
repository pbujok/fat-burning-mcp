# Fat Burning MCP

Fat Burning MCP exposes fitness activities over a Model Context Protocol HTTP endpoint and a small HTTP endpoint. Select either Strava or Garmin Connect Personal API as the activity provider.

## Garmin Connect Personal API

The Garmin adapter uses the official [Garmin Connect Personal API](https://developer.garmin.com/gc-developer-program/personal/swagger-docs) to search activities, retrieve each activity's details, and preserve the details JSON in the cache. It converts distance from meters to kilometers and uses Garmin's local activity start date and time.

Per-sample data (heart rate, GPS track, cadence, speed, power and every other numeric field the device recorded) is read on request, one activity at a time, through the `GetActivityStreams` MCP tool or `GET /items/{activityId}/streams?maxPoints=500`. The Personal API has no stream endpoint, so the adapter downloads the activity's original file and decodes its FIT data, then caches the samples. Omit `maxPoints` on the HTTP endpoint for every sample; the MCP tool returns 500 evenly spaced samples by default. Timestamps are UTC, and running cadence is per leg as recorded in FIT, so double it for steps per minute. Activities with no file, or uploaded as GPX or TCX, return no samples.

FIT decoding uses Garmin's [FIT SDK](https://www.nuget.org/packages/Garmin.FIT.Sdk), which has its own proprietary license and restricts redistribution, unlike this repository's MIT license. Review it before publishing a built image or binaries.

Create a Personal API key in Garmin Connect Web and treat it like a password. Garmin displays it only once; keys expire after the period selected when created (30, 90, or 180 days, or one year). The adapter exchanges the key for a new access token on each read; access tokens last up to 24 hours or until the key expires, and Garmin does not support refresh tokens. Garmin's Personal API is for accessing your own data and is not intended for commercial applications serving multiple users.

## Run with Docker Compose

Set provider credentials in a `.env` file in the project root. `ACTIVITY_PROVIDER` defaults to `Strava`; for Garmin, set `ACTIVITY_PROVIDER=Garmin` and `GARMIN_API_KEY` to your personal API key. For Strava, set `STRAVA_CLIENT_ID`, `STRAVA_CLIENT_SECRET`, and `STRAVA_REFRESH_TOKEN`. If local port `8137` is already in use, set `HOST_PORT`.

Build and start the service:

```powershell
docker compose up --build -d
```

The MCP endpoint is available at `http://localhost:8137/mcp`; the activity endpoint is `http://localhost:8137/items`. The SQLite cache is stored in the `activity-data` named volume. Strava uses `/data/fat-burning.db`; Garmin uses the separate `/data/garmin-activity.db` cache.

The service is bound to `127.0.0.1` because it does not provide authentication. Do not expose it to an untrusted network without adding an appropriate authentication and access-control layer.

To stop the service while retaining the cache, run `docker compose down`. To also remove the cache volume, run `docker compose down --volumes`.
# RR1X24 Frontend

Angular single-page application for the RR1X24 sports platform.

## Run locally
1. Install a compatible Node.js runtime and npm.
2. From this directory, run `npm ci`.
3. Set the backend URL in `proxy.conf.json` for local development.
4. Run `npm run start`.
5. Open `http://localhost:4200`.

For a production build, set `apiBaseUrl` in `src/environments/environment.production.ts` to your deployed backend and run `npm run build`.

The upstream project uses simulated matches and is not verified for production real-money betting. Never put secrets in frontend code.

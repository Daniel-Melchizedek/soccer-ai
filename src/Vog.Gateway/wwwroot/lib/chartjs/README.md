# Chart.js

Pinned version: 4.5.1, MIT. The unmodified `dist/chart.umd.min.js` and `LICENSE.md`
are copied from the npm `chart.js@4.5.1` package. This bundle includes its runtime
dependency and needs no CDN or build step in the gateway.

To update, install an explicitly selected version into a temporary directory
using `npm install --prefix <temp-directory> --ignore-scripts --save-exact chart.js@<version>`,
then copy the official UMD distribution and licence here. Run the browser tests
and verify the actual canvases before release.
const appJson = require('./app.json');

const config = appJson.expo;

module.exports = {
  ...config,
  plugins: [
    ...(config.plugins ?? []),
    [
      'expo-build-properties',
      {
        android: {
          // Kurum içi API HTTP üzerinde çalışabilir; release APK'da cleartext gerekir.
          usesCleartextTraffic: true,
        },
      },
    ],
  ],
  android: {
    ...config.android,
    googleServicesFile:
      process.env.GOOGLE_SERVICES_JSON ?? './google-services.json',
  },
};

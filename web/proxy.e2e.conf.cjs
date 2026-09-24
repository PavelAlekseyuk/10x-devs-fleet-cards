const target = process.env.FLEETCARDS_API_URL;

if (!target) {
  throw new Error('FLEETCARDS_API_URL is required for the isolated E2E proxy.');
}

module.exports = {
  '/api': {
    target,
    secure: false,
    changeOrigin: true,
    logLevel: 'warn',
  },
};
// This script runs only when initializing Report Service's own empty volume.
db = db.getSiblingDB(process.env.REPORT_MONGO_DATABASE);
db.createUser({ user: process.env.REPORT_MONGO_USER, pwd: process.env.REPORT_MONGO_PASSWORD,
  roles: [{ role: 'readWrite', db: process.env.REPORT_MONGO_DATABASE }] });

const database = db.getSiblingDB(process.env.AUDIT_MONGO_DATABASE || 'climate_audit');
database.createUser({user: process.env.AUDIT_MONGO_USER || 'audit_app', pwd: process.env.AUDIT_MONGO_PASSWORD,
  roles: [{role:'readWrite', db:database.getName()}]});

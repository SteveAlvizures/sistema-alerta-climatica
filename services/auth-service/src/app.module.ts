import { Module } from '@nestjs/common';
import { Database } from './persistence/database';
import { Tokens } from './auth/tokens';
import { AuthController } from './auth/auth.controller';
import { AuthService } from './auth/auth.service';
import { CredentialVerifier, LocalIdentityCredentials } from './identity/credential-verifier';
import { UsersController } from './users/users.controller';
import { UsersService } from './users/users.service';
import { IdentityController } from './identity/identity.controller';
import { AdministratorGuard, IdentityGuard } from './identity/identity.guard';
import { InternalGuard } from './identity/internal.guard';
import { Outbox } from './integration/outbox';
import { HealthController } from './health/health.controller';
import { OAuthController } from './auth/oauth.controller';
import { OAuthService } from './auth/oauth.service';
@Module({controllers:[AuthController,OAuthController,UsersController,IdentityController,HealthController],
  providers:[Database,Tokens,AuthService,OAuthService,{provide:CredentialVerifier,useClass:LocalIdentityCredentials},UsersService,IdentityGuard,AdministratorGuard,InternalGuard,Outbox]})
export class AppModule {}

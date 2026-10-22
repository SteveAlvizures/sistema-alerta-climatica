import { Injectable } from '@nestjs/common';
import { IdentityPasswords } from '../auth/passwords';

// Phase 4 extension point; no OAuth/OIDC endpoints or providers are implemented.
export abstract class CredentialVerifier {
  abstract verify(storedHash:string,password:string):Promise<boolean>;
}
@Injectable()
export class LocalIdentityCredentials extends CredentialVerifier {
  verify(storedHash:string,password:string):Promise<boolean> {return new IdentityPasswords().verify(storedHash,password);}
}

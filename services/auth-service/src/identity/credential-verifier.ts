import { Injectable } from '@nestjs/common';
import { IdentityPasswords } from '../auth/passwords';

// Local passwords retain their existing .NET-compatible format; OAuth uses external accounts.
export abstract class CredentialVerifier {
  abstract verify(storedHash:string,password:string):Promise<boolean>;
}
@Injectable()
export class LocalIdentityCredentials extends CredentialVerifier {
  verify(storedHash:string,password:string):Promise<boolean> {return new IdentityPasswords().verify(storedHash,password);}
}

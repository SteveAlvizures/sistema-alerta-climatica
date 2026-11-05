import { Injectable } from '@nestjs/common';
import { IdentityPasswords } from '../auth/passwords';
import { compare } from 'bcryptjs';

// Local passwords retain their existing .NET-compatible format; OAuth uses external accounts.
export abstract class CredentialVerifier {
  abstract verify(storedHash:string,password:string):Promise<boolean>;
}
@Injectable()
export class LocalIdentityCredentials extends CredentialVerifier {
  async verify(storedHash:string,password:string):Promise<boolean> {
    // Preserve imported Astrid accounts without changing hashes or weakening new password storage.
    if (/^\$2[aby]\$/.test(storedHash)) {
      try { return await compare(password,storedHash); } catch { return false; }
    }
    return new IdentityPasswords().verify(storedHash,password);
  }
}

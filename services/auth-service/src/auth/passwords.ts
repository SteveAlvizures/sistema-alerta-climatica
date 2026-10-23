import { pbkdf2, randomBytes, timingSafeEqual } from 'node:crypto';
import { promisify } from 'node:util';
const derive = promisify(pbkdf2);

/**
 * Preserves ASP.NET Identity V2/V3 hashes so migration does not require password resets.
 * V3 stores PRF, iteration count and salt length as big-endian UInt32 values.
 * Bounds on imported headers prevent an untrusted hash from requesting unbounded PBKDF2 work.
 */
export class IdentityPasswords {
  async verify(encoded: string, password: string): Promise<boolean> {
    try {
      if (!encoded || encoded.length > 500 || !/^[A-Za-z0-9+/]+={0,2}$/.test(encoded) || encoded.length % 4 !== 0) return false;
      const bytes = Buffer.from(encoded, 'base64');
      if (bytes.toString('base64') !== encoded) return false;
      let iterations: number, algorithm: string, salt: Buffer, expected: Buffer;
      if (bytes[0] === 0 && bytes.length === 49) {
        iterations = 1000; algorithm = 'sha1'; salt = bytes.subarray(1,17); expected = bytes.subarray(17);
      } else if (bytes[0] === 1 && bytes.length >= 45) {
        const prf = bytes.readUInt32BE(1); iterations = bytes.readUInt32BE(5);
        const saltLength = bytes.readUInt32BE(9);
        algorithm = ['sha1','sha256','sha512'][prf];
        if (!algorithm || iterations < 1 || iterations > 2_000_000 || saltLength < 16 || saltLength > bytes.length - 29) return false;
        salt = bytes.subarray(13,13+saltLength); expected = bytes.subarray(13+saltLength);
      } else return false;
      const actual = await derive(password, salt, iterations, expected.length, algorithm);
      return timingSafeEqual(actual, expected);
    } catch { return false; }
  }
  async hash(password: string): Promise<string> {
    // New credentials retain the default .NET V3 format: SHA512, 100000 iterations, 16-byte salt.
    const salt = randomBytes(16);
    const header = Buffer.alloc(13);
    header[0]=1; header.writeUInt32BE(2,1); header.writeUInt32BE(100000,5); header.writeUInt32BE(16,9);
    return Buffer.concat([header,salt,await derive(password,salt,100000,32,'sha512')]).toString('base64');
  }
}

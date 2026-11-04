import {
  BadRequestException,
  Injectable,
  UnauthorizedException,
} from '@nestjs/common';

import { ConfigService } from '@nestjs/config';
import { JwtService } from '@nestjs/jwt';
import { InjectRepository } from '@nestjs/typeorm';

import * as bcrypt from 'bcrypt';
import { randomUUID } from 'crypto';
import { Repository } from 'typeorm';
import { OAuth2Client } from 'google-auth-library';

import { UsersService } from '../users/users.service.js';
import { User } from '../users/entities/user.entity.js';
import { Role } from '../roles/entities/role.entity.js';

import {
  ExternalAccount,
  ExternalProvider,
} from './entities/external-account.entity.js';

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  name: string;
  email: string;
  role: string;
  provider: string;
}

interface OAuthStatePayload {
  purpose: string;
  provider: ExternalProvider;
}

interface GoogleTokenResponse {
  id_token?: string;
  error?: string;
  error_description?: string;
}

interface GitHubTokenResponse {
  access_token?: string;
  error?: string;
  error_description?: string;
}

interface GitHubUser {
  id: number;
  login: string;
  name: string | null;
  email: string | null;
}

interface GitHubEmail {
  email: string;
  primary: boolean;
  verified: boolean;
}

@Injectable()
export class AuthService {
  private readonly googleClient: OAuth2Client;

  constructor(
    private readonly usersService: UsersService,
    private readonly jwtService: JwtService,
    private readonly configService: ConfigService,

    @InjectRepository(User)
    private readonly usersRepository: Repository<User>,

    @InjectRepository(Role)
    private readonly rolesRepository: Repository<Role>,

    @InjectRepository(ExternalAccount)
    private readonly externalAccountsRepository: Repository<ExternalAccount>,
  ) {
    this.googleClient = new OAuth2Client(
      this.configService.get<string>('GOOGLE_CLIENT_ID'),
    );
  }

  async login(
    request: LoginRequest,
  ): Promise<LoginResponse> {
    const username =
      request.username?.trim().toLowerCase() ?? '';

    const password =
      request.password ?? '';

    if (!username || !password) {
      throw new UnauthorizedException(
        'Usuario o contraseña incorrectos.',
      );
    }

    let user =
      await this.usersService.findByUsername(
        username,
      );

    if (!user) {
      const matches =
        await this.usersService.findByName(
          username,
        );

      user =
        matches.length === 1
          ? matches[0]
          : null;
    }

    if (!user || !user.isActive) {
      throw new UnauthorizedException(
        'Usuario o contraseña incorrectos.',
      );
    }

    if (!user.passwordHash) {
      throw new UnauthorizedException(
        'Esta cuenta utiliza autenticación externa.',
      );
    }

    const passwordValid =
      await bcrypt.compare(
        password,
        user.passwordHash,
      );

    if (!passwordValid) {
      throw new UnauthorizedException(
        'Usuario o contraseña incorrectos.',
      );
    }

    return this.issueInternalJwt(
      user,
      'local',
    );
  }

  async getGoogleAuthorizationUrl():
    Promise<string> {
    const clientId =
      this.configService.getOrThrow<string>(
        'GOOGLE_CLIENT_ID',
      );

    const callbackUrl =
      this.configService.getOrThrow<string>(
        'GOOGLE_CALLBACK_URL',
      );

    const state =
      await this.createOAuthState(
        'google',
      );

    const params =
      new URLSearchParams({
        client_id: clientId,
        redirect_uri: callbackUrl,
        response_type: 'code',
        scope: 'openid email profile',
        state,
        prompt: 'select_account',
      });

    return (
      'https://accounts.google.com/o/oauth2/v2/auth?' +
      params.toString()
    );
  }

  async loginWithGoogleCallback(
    code: string,
    state: string,
  ): Promise<LoginResponse> {
    await this.validateOAuthState(
      state,
      'google',
    );

    const clientId =
      this.configService.getOrThrow<string>(
        'GOOGLE_CLIENT_ID',
      );

    const clientSecret =
      this.configService.getOrThrow<string>(
        'GOOGLE_CLIENT_SECRET',
      );

    const callbackUrl =
      this.configService.getOrThrow<string>(
        'GOOGLE_CALLBACK_URL',
      );

    const response =
      await fetch(
        'https://oauth2.googleapis.com/token',
        {
          method: 'POST',
          headers: {
            'Content-Type':
              'application/x-www-form-urlencoded',
          },
          body: new URLSearchParams({
            code,
            client_id: clientId,
            client_secret: clientSecret,
            redirect_uri: callbackUrl,
            grant_type:
              'authorization_code',
          }),
        },
      );

    const tokenData =
      (await response.json()) as GoogleTokenResponse;

    if (
      !response.ok ||
      !tokenData.id_token
    ) {
      throw new UnauthorizedException(
        tokenData.error_description ??
          'No fue posible autenticar con Google.',
      );
    }

    const ticket =
      await this.googleClient.verifyIdToken({
        idToken:
          tokenData.id_token,
        audience:
          clientId,
      });

    const payload =
      ticket.getPayload();

    if (
      !payload?.sub ||
      !payload.email ||
      payload.email_verified !== true
    ) {
      throw new UnauthorizedException(
        'Google no proporcionó una identidad válida.',
      );
    }

    return this.finishExternalLogin(
      'google',
      payload.sub,
      payload.email,
      payload.name ??
        payload.email,
    );
  }

  async getGitHubAuthorizationUrl():
    Promise<string> {
    const clientId =
      this.configService.getOrThrow<string>(
        'GITHUB_CLIENT_ID',
      );

    const callbackUrl =
      this.configService.getOrThrow<string>(
        'GITHUB_CALLBACK_URL',
      );

    const state =
      await this.createOAuthState(
        'github',
      );

    const params =
      new URLSearchParams({
        client_id:
          clientId,
        redirect_uri:
          callbackUrl,
        scope:
          'read:user user:email',
        state,
      });

    return (
      'https://github.com/login/oauth/authorize?' +
      params.toString()
    );
  }

  async loginWithGitHubCallback(
    code: string,
    state: string,
  ): Promise<LoginResponse> {
    await this.validateOAuthState(
      state,
      'github',
    );

    const clientId =
      this.configService.getOrThrow<string>(
        'GITHUB_CLIENT_ID',
      );

    const clientSecret =
      this.configService.getOrThrow<string>(
        'GITHUB_CLIENT_SECRET',
      );

    const callbackUrl =
      this.configService.getOrThrow<string>(
        'GITHUB_CALLBACK_URL',
      );

    const tokenResponse =
      await fetch(
        'https://github.com/login/oauth/access_token',
        {
          method: 'POST',
          headers: {
            Accept:
              'application/json',
            'Content-Type':
              'application/x-www-form-urlencoded',
          },
          body: new URLSearchParams({
            client_id:
              clientId,
            client_secret:
              clientSecret,
            code,
            redirect_uri:
              callbackUrl,
          }),
        },
      );

    const tokenData =
      (await tokenResponse.json()) as GitHubTokenResponse;

    if (
      !tokenResponse.ok ||
      !tokenData.access_token
    ) {
      throw new UnauthorizedException(
        tokenData.error_description ??
          'No fue posible autenticar con GitHub.',
      );
    }

    const headers = {
      Authorization:
        `Bearer ${tokenData.access_token}`,
      Accept:
        'application/vnd.github+json',
      'User-Agent':
        'vigia-rural-auth-service',
    };

    const userResponse =
      await fetch(
        'https://api.github.com/user',
        {
          headers,
        },
      );

    if (!userResponse.ok) {
      throw new UnauthorizedException(
        'No fue posible obtener el usuario de GitHub.',
      );
    }

    const githubUser =
      (await userResponse.json()) as GitHubUser;

    let email =
      githubUser.email;

    if (!email) {
      const emailResponse =
        await fetch(
          'https://api.github.com/user/emails',
          {
            headers,
          },
        );

      if (emailResponse.ok) {
        const emails =
          (await emailResponse.json()) as GitHubEmail[];

        const selected =
          emails.find(
            (item) =>
              item.primary &&
              item.verified,
          ) ??
          emails.find(
            (item) =>
              item.verified,
          );

        email =
          selected?.email ??
          null;
      }
    }

    if (!email) {
      throw new UnauthorizedException(
        'GitHub no proporcionó un correo electrónico verificado.',
      );
    }

    const name =
      githubUser.name?.trim() ||
      githubUser.login;

    return this.finishExternalLogin(
      'github',
      String(
        githubUser.id,
      ),
      email,
      name,
    );
  }

  getFrontendRedirectUrl(
    response: LoginResponse,
  ): string {
    const baseUrl =
      this.configService.getOrThrow<string>(
        'FRONTEND_OAUTH_SUCCESS_URL',
      );

    const params =
      new URLSearchParams({
        accessToken:
          response.accessToken,
        expiresAt:
          response.expiresAt,
        provider:
          response.provider,
      });

    return (
      `${baseUrl}#` +
      params.toString()
    );
  }

  private async finishExternalLogin(
    provider: ExternalProvider,
    providerUserId: string,
    email: string,
    name: string,
  ): Promise<LoginResponse> {
    const normalizedEmail =
      email.trim().toLowerCase();

    let externalAccount =
      await this.externalAccountsRepository
        .findOne({
          where: {
            provider,
            providerUserId,
          },
          relations: {
            user: {
              role: {
                permissions: true,
              },
            },
          },
        });

    let user: User | null;

    if (externalAccount) {
      user =
        externalAccount.user;
    } else {
      user =
        await this.usersRepository.findOne({
          where: {
            email:
              normalizedEmail,
          },
          relations: {
            role: {
              permissions: true,
            },
          },
        });

      if (!user) {
        const viewerRole =
          await this.rolesRepository.findOne({
            where: {
              name: 'VIEWER',
            },
            relations: {
              permissions: true,
            },
          });

        if (!viewerRole) {
          throw new BadRequestException(
            'No existe el rol VIEWER.',
          );
        }

        user =
          this.usersRepository.create({
            name:
              name.trim(),
            email:
              normalizedEmail,
            passwordHash:
              null,
            role:
              viewerRole,
            isActive:
              true,
            lastAccessAt:
              null,
          });

        user =
          await this.usersRepository.save(
            user,
          );
      }

      externalAccount =
        this.externalAccountsRepository.create({
          user,
          provider,
          providerUserId,
          providerEmail:
            normalizedEmail,
        });

      await this.externalAccountsRepository.save(
        externalAccount,
      );
    }

    if (!user.isActive) {
      throw new UnauthorizedException(
        'La cuenta está deshabilitada.',
      );
    }

    return this.issueInternalJwt(
      user,
      provider,
    );
  }

  private async issueInternalJwt(
    user: User,
    provider: string,
  ): Promise<LoginResponse> {
    const completeUser =
      await this.usersRepository.findOne({
        where: {
          id:
            user.id,
        },
        relations: {
          role: {
            permissions: true,
          },
        },
      });

    if (!completeUser) {
      throw new UnauthorizedException(
        'Usuario no encontrado.',
      );
    }

    const permissions =
      completeUser.role.permissions
        ?.map(
          (permission) =>
            permission.name,
        ) ?? [];

    const expiresIn =
      this.configService.get<string>(
        'JWT_EXPIRES_IN',
      ) ?? '1h';

    const issuer =
      this.configService.getOrThrow<string>(
        'JWT_ISSUER',
      );

    const audience =
      this.configService.getOrThrow<string>(
        'JWT_AUDIENCE',
      );

    const accessToken =
      await this.jwtService.signAsync(
        {
          sub:
            completeUser.id,
          email:
            completeUser.email,
          name:
            completeUser.name,
          role:
            completeUser.role.name,
          roles: [
            completeUser.role.name,
          ],
          permissions,
          provider,
          jti:
            randomUUID(),
        },
        {
          issuer,
          audience,
          expiresIn:
            expiresIn as any,
        },
      );

    const decoded =
      this.jwtService.decode(
        accessToken,
      );

    const expiresAt =
      typeof decoded === 'object' &&
      decoded?.exp
        ? new Date(
            decoded.exp *
              1000,
          ).toISOString()
        : new Date().toISOString();

    await this.usersRepository.update(
      completeUser.id,
      {
        lastAccessAt:
          new Date(),
      },
    );

    if (
      provider === 'google' ||
      provider === 'github'
    ) {
      await this.registerLoginAudit(
        completeUser,
        provider,
        issuer,
        audience,
      );
    }

    return {
      accessToken,
      expiresAt,
      name:
        completeUser.name,
      email:
        completeUser.email,
      role:
        completeUser.role.name,
      provider,
    };
  }

  private async registerLoginAudit(
    user: User,
    provider: ExternalProvider,
    issuer: string,
    audience: string,
  ): Promise<void> {
    try {
      /*
       * Token interno de corta duración utilizado únicamente
       * para registrar el evento en Audit Service.
       */
      const auditToken =
        await this.jwtService.signAsync(
          {
            sub:
              user.id,
            email:
              user.email,
            username:
              user.email,
            name:
              user.name,
            role:
              'SYSTEM',
            roles: [
              'SYSTEM',
            ],
            permissions: [
              'audit.write',
            ],
            provider:
              'internal',
            jti:
              randomUUID(),
          },
          {
            issuer,
            audience,
            expiresIn:
              '2m',
          },
        );

      const action =
        provider === 'google'
          ? 'LOGIN_GOOGLE_SUCCESS'
          : 'LOGIN_GITHUB_SUCCESS';

      const response =
        await fetch(
          'http://audit-service:3006/api/audit-events',
          {
            method: 'POST',
            headers: {
              Authorization:
                `Bearer ${auditToken}`,
              'Content-Type':
                'application/json',
            },
            body:
              JSON.stringify({
                action,
                entity_type:
                  'AUTHENTICATION',
                entity_id:
                  user.id,
                description:
                  `Inicio de sesión exitoso mediante ${provider}.`,
                metadata: {
                  provider,
                  email:
                    user.email,
                  role:
                    user.role?.name ??
                    'UNKNOWN',
                },
              }),
          },
        );

      if (!response.ok) {
        console.warn(
          `No fue posible registrar auditoría de login ${provider}. HTTP ${response.status}`,
        );
      }
    } catch (error) {
      /*
       * Una falla de auditoría no debe impedir
       * que el usuario pueda autenticarse.
       */
      console.warn(
        'No fue posible registrar el evento de auditoría:',
        error,
      );
    }
  }

  private async createOAuthState(
    provider: ExternalProvider,
  ): Promise<string> {
    return this.jwtService.signAsync(
      {
        purpose:
          'oauth-state',
        provider,
      },
      {
        expiresIn:
          '10m',
      },
    );
  }

  private async validateOAuthState(
    state: string,
    expectedProvider:
      ExternalProvider,
  ): Promise<void> {
    if (!state) {
      throw new UnauthorizedException(
        'Estado OAuth inválido.',
      );
    }

    try {
      const payload =
        await this.jwtService.verifyAsync<OAuthStatePayload>(
          state,
        );

      if (
        payload.purpose !==
          'oauth-state' ||
        payload.provider !==
          expectedProvider
      ) {
        throw new Error(
          'OAuth state mismatch',
        );
      }
    } catch {
      throw new UnauthorizedException(
        'Estado OAuth inválido o expirado.',
      );
    }
  }
}

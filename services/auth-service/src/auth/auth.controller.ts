import {
  Body,
  Controller,
  Get,
  HttpCode,
  HttpStatus,
  Post,
  Query,
  Redirect,
  Res,
  UseGuards,
} from '@nestjs/common';

import type {
  Response,
} from 'express';

import { AuthService } from './auth.service.js';
import type {
  LoginResponse,
} from './auth.service.js';

import { LoginDto } from './dto/login.dto.js';

import { JwtAuthGuard } from './guards/jwt-auth.guard.js';

@Controller('api/auth')
export class AuthController {
  constructor(
    private readonly authService:
      AuthService,
  ) {}

  @Post('login')
  @HttpCode(HttpStatus.OK)
  login(
    @Body()
    request: LoginDto,
  ): Promise<LoginResponse> {
    return this.authService
      .login(request);
  }

  @Get('oauth/google')
  @Redirect()
  async googleLogin() {
    return {
      url:
        await this.authService
          .getGoogleAuthorizationUrl(),

      statusCode:
        HttpStatus.FOUND,
    };
  }

  @Get('callback/google')
  async googleCallback(
    @Query('code')
    code: string,

    @Query('state')
    state: string,

    @Res()
    response: Response,
  ): Promise<void> {
    const result =
      await this.authService
        .loginWithGoogleCallback(
          code,
          state,
        );

    response.redirect(
      HttpStatus.FOUND,

      this.authService
        .getFrontendRedirectUrl(
          result,
        ),
    );
  }

  @Get('oauth/github')
  @Redirect()
  async githubLogin() {
    return {
      url:
        await this.authService
          .getGitHubAuthorizationUrl(),

      statusCode:
        HttpStatus.FOUND,
    };
  }

  @Get('callback/github')
  async githubCallback(
    @Query('code')
    code: string,

    @Query('state')
    state: string,

    @Res()
    response: Response,
  ): Promise<void> {
    const result =
      await this.authService
        .loginWithGitHubCallback(
          code,
          state,
        );

    response.redirect(
      HttpStatus.FOUND,

      this.authService
        .getFrontendRedirectUrl(
          result,
        ),
    );
  }

  @Post('logout')
  @UseGuards(JwtAuthGuard)
  @HttpCode(
    HttpStatus.NO_CONTENT,
  )
  logout(): void {
    return;
  }
}

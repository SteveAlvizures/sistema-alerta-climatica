import { Body, Controller, HttpCode, Post, Req, UseGuards } from '@nestjs/common';
import { AuthRequest, IdentityGuard } from '../identity/identity.guard';
import { AuthService } from './auth.service';
@Controller('api/auth')
export class AuthController {
  constructor(private readonly auth:AuthService) {}
  @Post('login') @HttpCode(200)
  async login(@Body() body:Record<string,unknown>) {
    return this.auth.login(body);
  }
  @Post('logout') @HttpCode(204) @UseGuards(IdentityGuard)
  async logout(@Req() request:AuthRequest) {
    await this.auth.logout(request.identity);
  }
}

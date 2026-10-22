import { Controller, Get, Req, UseGuards } from '@nestjs/common';
import { AuthRequest, IdentityGuard } from './identity.guard';
import { InternalGuard } from './internal.guard';
@Controller('internal/identity')
@UseGuards(InternalGuard, IdentityGuard)
export class IdentityController {
  @Get('current') current(@Req() request:AuthRequest) { return request.identity; }
}

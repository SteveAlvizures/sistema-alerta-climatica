import {
  Body,
  Controller,
  Get,
  Param,
  Patch,
  Post,
  Put,
  Req,
  UseGuards,
} from '@nestjs/common';

import { JwtAuthGuard } from '../auth/guards/jwt-auth.guard.js';
import { RolesGuard } from '../auth/guards/roles.guard.js';
import { PermissionsGuard } from '../auth/guards/permissions.guard.js';

import { Roles } from '../auth/decorators/roles.decorator.js';
import { Permissions } from '../auth/decorators/permissions.decorator.js';

import { UsersService } from './users.service.js';

import { CreateUserDto } from './dto/create-user.dto.js';
import { UpdateUserDto } from './dto/update-user.dto.js';
import { ChangeUserStatusDto } from './dto/change-user-status.dto.js';
import { ChangeUserRoleDto } from './dto/change-user-role.dto.js';

interface AuthenticatedRequest {
  user: {
    id: string;
    email: string;
    name: string;
    role: string;
    permissions: string[];
    jti: string;
  };
}

@Controller('api/users')
@UseGuards(
  JwtAuthGuard,
  RolesGuard,
  PermissionsGuard,
)
@Roles('Administrator')
export class UsersController {
  constructor(
    private readonly usersService: UsersService,
  ) {}

  @Get()
  @Permissions('users.read')
  findAll() {
    return this.usersService.findAll();
  }

  @Get(':id')
  @Permissions('users.read')
  findById(
    @Param('id') id: string,
  ) {
    return this.usersService.getById(id);
  }

  @Post()
  @Permissions('users.write')
  create(
    @Body() dto: CreateUserDto,
  ) {
    return this.usersService.create(dto);
  }

  @Put(':id')
  @Permissions('users.write')
  update(
    @Param('id') id: string,
    @Body() dto: UpdateUserDto,
  ) {
    return this.usersService.updateUser(
      id,
      dto,
    );
  }

  @Patch(':id/status')
  @Permissions('users.write')
  changeStatus(
    @Param('id') id: string,
    @Body() dto: ChangeUserStatusDto,
    @Req() request: AuthenticatedRequest,
  ) {
    return this.usersService.changeStatus(
      id,
      dto.isActive,
      request.user.id,
    );
  }

  @Patch(':id/role')
  @Permissions('users.write')
  changeRole(
    @Param('id') id: string,
    @Body() dto: ChangeUserRoleDto,
    @Req() request: AuthenticatedRequest,
  ) {
    return this.usersService.changeRole(
      id,
      dto.role,
      request.user.id,
    );
  }
}

import { Body, Controller, Get, HttpCode, Param, ParseUUIDPipe, Patch, Post, Put, Query, Req, Res, UseGuards } from '@nestjs/common';
import { Response } from 'express';
import { AdministratorGuard, AuthRequest, IdentityGuard } from '../identity/identity.guard';
import { UsersService } from './users.service';
@Controller('api/users')
@UseGuards(IdentityGuard,AdministratorGuard)
export class UsersController {
  constructor(private readonly users:UsersService) {}
  @Get() list(@Query() query:Record<string,unknown>) {return this.users.list(query);}
  @Get(':id') get(@Param('id',new ParseUUIDPipe({errorHttpStatusCode:404})) id:string) {return this.users.get(id);}
  @Post() async create(@Body() body:Record<string,unknown>,@Req() request:AuthRequest,@Res({passthrough:true}) res:Response) {
    const user=await this.users.create(body??{},request.identity);res.location('/api/users/'+user.id);return user;
  }
  @Put(':id') update(@Param('id',new ParseUUIDPipe({errorHttpStatusCode:404})) id:string,@Body() body:Record<string,unknown>,@Req() request:AuthRequest) {return this.users.update(id,body??{},request.identity);}
  @Patch(':id/status') status(@Param('id',new ParseUUIDPipe({errorHttpStatusCode:404})) id:string,@Body() body:Record<string,unknown>,@Req() request:AuthRequest) {return this.users.change(id,body??{},request.identity,'status');}
  @Patch(':id/role') role(@Param('id',new ParseUUIDPipe({errorHttpStatusCode:404})) id:string,@Body() body:Record<string,unknown>,@Req() request:AuthRequest) {return this.users.change(id,body??{},request.identity,'role');}
}

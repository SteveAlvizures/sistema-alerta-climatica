import { Body, Controller, Get, HttpCode, Param, Post, Query, Req, Res, UnauthorizedException } from '@nestjs/common';
import { Request, Response } from 'express';
import { OAuthService, oauthOrigin, Provider } from './oauth.service';

function provider(value:string):Provider { if(value!=='google' && value!=='github') throw new UnauthorizedException();return value; }
function browser(req:Request):string|undefined {
  const values=(req.headers.cookie??'').split(';').map(v=>v.trim()).filter(v=>v.startsWith('vigia_oauth='));
  return values.length===1?values[0].slice('vigia_oauth='.length):undefined;
}
function options() {return {httpOnly:true,secure:oauthOrigin().startsWith('https:'),sameSite:'lax' as const,path:'/api/auth',maxAge:600000};}
@Controller('api/auth')
export class OAuthController {
  constructor(private readonly oauth:OAuthService) {}
  @Get('oauth/:provider')
  async start(@Param('provider') value:string,@Res() res:Response) {
    const result=await this.oauth.start(provider(value));
    res.setHeader('Cache-Control','no-store');res.setHeader('Referrer-Policy','no-referrer');
    res.cookie('vigia_oauth',result.browser,options());res.redirect(302,result.url);
  }
  @Get('callback/:provider')
  async callback(@Param('provider') value:string,@Query('code') code:unknown,@Query('state') state:unknown,@Req() req:Request,@Res() res:Response) {
    res.setHeader('Cache-Control','no-store');res.setHeader('Referrer-Policy','no-referrer');
    res.redirect(302,await this.oauth.callback(provider(value),code,state,browser(req)));
  }
  @Post('oauth/exchange') @HttpCode(200)
  async exchange(@Body() body:Record<string,unknown>,@Req() req:Request,@Res({passthrough:true}) res:Response) {
    if(req.headers.origin!==oauthOrigin()) throw new UnauthorizedException();
    res.setHeader('Cache-Control','no-store');res.setHeader('Referrer-Policy','no-referrer');
    const result=await this.oauth.exchange(body?.code,browser(req));
    const {maxAge:_,...clearOptions}=options();
    res.clearCookie('vigia_oauth',clearOptions);return result;
  }
}

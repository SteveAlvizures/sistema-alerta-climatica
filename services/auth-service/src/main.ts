import 'reflect-metadata';
import { NestFactory } from '@nestjs/core';
import { randomUUID } from 'node:crypto';
import { Request, Response, NextFunction } from 'express';
import { AppModule } from './app.module';
import { ProblemFilter } from './common/problem.filter';

export async function createApp() {
  const app=await NestFactory.create(AppModule,{logger:false});
  app.getHttpAdapter().getInstance().disable('x-powered-by');
  app.use((req:Request,res:Response,next:NextFunction)=>{
    const supplied=req.headers['x-correlation-id'];
    const id=typeof supplied==='string'&&/^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(supplied)?supplied.toLowerCase():randomUUID();
    req.headers['x-correlation-id']=id;res.setHeader('X-Correlation-ID',id);
    const start=Date.now();
    res.on('finish',()=>console.info(JSON.stringify({correlationId:id,method:req.method,status:res.statusCode,elapsedMs:Date.now()-start})));
    next();
  });
  app.useGlobalFilters(new ProblemFilter());
  app.enableShutdownHooks();
  return app;
}
if(require.main===module) {
  createApp().then(app=>app.listen(Number(process.env.PORT??3000),'0.0.0.0')).catch(()=>{
    console.error('Auth Service could not start. Check configuration and database readiness.');process.exitCode=1;
  });
}

import { ArgumentsHost, Catch, ExceptionFilter, HttpException } from '@nestjs/common';
import { Request, Response } from 'express';

@Catch()
export class ProblemFilter implements ExceptionFilter {
  catch(error: unknown, host: ArgumentsHost): void {
    const response = host.switchToHttp().getResponse<Response>();
    const request = host.switchToHttp().getRequest<Request>();
    let status = error instanceof HttpException ? error.getStatus() : 500;
    if ((error as { code?: string })?.code === '23505') status = 409;
    if (['57P01','57P02','57P03','ECONNREFUSED','ECONNRESET','ETIMEDOUT'].includes((error as {code?:string})?.code??'')) status=503;
    const payload = error instanceof HttpException ? error.getResponse() : null;
    if (status === 401 && typeof payload === 'object' && payload && 'message' in payload && payload.message === 'Usuario o contraseña incorrectos.') {
      response.status(status).json({ message: payload.message }); return;
    }
    const detail = status === 500 ? 'Ocurrió un error interno al procesar la solicitud.'
      : status === 503 ? 'Servicio no disponible. Inténtalo de nuevo.'
      : status === 409 && !(error instanceof HttpException) ? 'El login ya está registrado.'
      : error instanceof HttpException ? error.message : 'Solicitud inválida.';
    response.status(status).type('application/problem+json').json({
      status, title: ({400:'Solicitud inválida',401:'No autorizado',403:'Acceso denegado',404:'Recurso no encontrado',409:'Conflicto'} as Record<number,string>)[status] ?? 'Error interno',
      detail, correlationId: request.headers['x-correlation-id'],
    });
  }
}

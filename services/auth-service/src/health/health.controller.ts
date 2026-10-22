import { Controller, Get, ServiceUnavailableException } from '@nestjs/common';
import { Database } from '../persistence/database';
@Controller('health')
export class HealthController {
  constructor(private readonly db:Database) {}
  @Get() live() {return {status:'Healthy',service:'ClimateAlert.Auth'};}
  @Get('live') liveness() {return this.live();}
  @Get('ready') async readiness() {
    try {await this.db.pool.query('SELECT 1');return this.live();}
    catch {throw new ServiceUnavailableException('Servicio no disponible.');}
  }
}

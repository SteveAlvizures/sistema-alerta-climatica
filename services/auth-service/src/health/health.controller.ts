import { Controller, Get } from '@nestjs/common';
import { DataSource } from 'typeorm';

@Controller('health')
export class HealthController {
  constructor(
    private readonly dataSource: DataSource,
  ) {}

  @Get()
  health() {
    return {
      status: 'ok',
      service: 'auth-service',
      database: this.dataSource.isInitialized
        ? 'connected'
        : 'disconnected',
      timestamp: new Date().toISOString(),
    };
  }
}

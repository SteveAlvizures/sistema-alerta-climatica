import { AlertDto } from '../models/api.model';
import { AlertNotifications } from './alert-notifications.service';
describe('Session alert notifications',()=>{
  it('shows a real water high alert once and retains its server identity',()=>{
    const service=new AlertNotifications();
    const high={id:'water-alert',eventId:'water-event',status:'Open',level:'Orange',variable:'RiverOrReservoirLevel',sensorCode:'ESP32-WATER-LEVEL-01',detectedValue:2,unit:'estado',communityName:'Santa Fe Zona 13',updatedAt:'2026-11-06T08:00:00Z'} as AlertDto;
    service.observe([high]);
    expect(service.notices()[0].title).toBe('Alerta · Nivel de agua alto en Santa Fe Zona 13');
    expect(service.notices()[0].detail).toContain('Nivel alto');
    service.dismiss(high.id);
    for(let i=0;i<10;i++) service.observe([high]);
    expect(service.notices().length).toBe(0);
  });
  beforeEach(()=>sessionStorage.removeItem('vigia-monitoring-notices-v1'));
  afterEach(()=>sessionStorage.removeItem('vigia-monitoring-notices-v1'));
  const alert={id:'a',eventId:'e',status:'Open',level:'Yellow',variable:'RainfallLevel',detectedValue:0,unit:'estado',communityName:'Santa Fe Zona 13',updatedAt:'2026-11-05T12:00:00Z'} as AlertDto;
  it('retains seen IDs across a page reload within the tab session',()=>{
    const first=new AlertNotifications();first.observe([alert]);first.dismiss('a');
    const reloaded=new AlertNotifications();reloaded.observe([alert]);expect(reloaded.notices().length).toBe(0);
    reloaded.observe([{...alert,level:'Orange'}]);expect(reloaded.notices().length).toBe(1);
  });
  it('notifies once, never spams a poll and reports escalation',()=>{
    const service=new AlertNotifications();service.observe([alert]);expect(service.notices().length).toBe(1);
    expect(service.notices()[0].detail).toContain('Seco');service.dismiss('a');
    for(let i=0;i<10;i++)service.observe([alert]);expect(service.notices().length).toBe(0);
    service.observe([{...alert,level:'Red'}]);expect(service.notices()[0].title).toContain('Emergencia');
    service.dismiss('a');service.observe([{...alert,level:'Orange'}]);expect(service.notices().length).toBe(0);
  });
  it('notifies a new alert within an existing event and ignores closed alerts',()=>{
    const service=new AlertNotifications();service.observe([alert]);service.dismiss('a');
    service.observe([{...alert,id:'b'}]);expect(service.notices().length).toBe(1);service.dismiss('b');
    service.observe([{...alert,id:'c',eventId:'new',status:'Closed'}]);expect(service.notices().length).toBe(0);
    service.observe([{...alert,id:'d',eventId:'new'}]);expect(service.notices().length).toBe(1);
  });
});

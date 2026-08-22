import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { AlertRuleDto, ClimateVariable, CommunityDto } from '../../../../core/models/api.model';
import { AlertRuleApiService, CreateAlertRuleRequest } from '../../../../core/services/alert-rule-api.service';
import { AuthService } from '../../../../core/services/auth.service';
import { CommunityApiService } from '../../../../core/services/community-api.service';

const variables: Record<ClimateVariable, string> = { Temperature:'Temperatura',RelativeHumidity:'Humedad relativa',WindSpeed:'Velocidad del viento',RainfallLevel:'Nivel de lluvia',RiverOrReservoirLevel:'Nivel de río o reservorio' };
@Component({selector:'app-alert-rules-page',imports:[FormsModule],templateUrl:'./alert-rules-page.html',styleUrl:'./alert-rules-page.scss'})
export class AlertRulesPage implements OnInit {
  private readonly api=inject(AlertRuleApiService); private readonly communitiesApi=inject(CommunityApiService); protected readonly auth=inject(AuthService);
  protected rules:AlertRuleDto[]=[]; protected communities:CommunityDto[]=[]; protected loading=true; protected saving=false; protected error=''; protected success='';
  protected communityId=''; protected code=''; protected name=''; protected variable:ClimateVariable='RainfallLevel'; protected phenomenon='Flood'; protected dangerLevel='Yellow'; protected lowerLimit:number|null=null; protected upperLimit:number|null=null;
  protected readonly variableOptions=Object.keys(variables) as ClimateVariable[];
  ngOnInit():void{forkJoin({rules:this.api.getAll(),communities:this.communitiesApi.getAll()}).subscribe({next:({rules,communities})=>{this.rules=rules;this.communities=communities;this.communityId=communities[0]?.id??'';this.loading=false},error:()=>{this.error='No fue posible cargar las reglas.';this.loading=false}})}
  protected canAdminister():boolean{return this.auth.session()?.role==='Administrator'}
  protected communityName(id:string):string{return this.communities.find(item=>item.id===id)?.name??'Comunidad no disponible'}
  protected variableLabel(value:ClimateVariable):string{return variables[value]}
  protected create():void{if(!this.canAdminister()||this.saving||!this.communityId||!this.code.trim()||!this.name.trim()||(this.lowerLimit===null&&this.upperLimit===null)){this.error='Completa comunidad, código, nombre y al menos un límite.';return} const request:CreateAlertRuleRequest={communityId:this.communityId,sensorId:null,code:this.code.trim(),name:this.name.trim(),phenomenon:this.phenomenon as CreateAlertRuleRequest['phenomenon'],variable:this.variable,dangerLevel:this.dangerLevel as CreateAlertRuleRequest['dangerLevel'],lowerLimit:this.lowerLimit,upperLimit:this.upperLimit,validFrom:new Date().toISOString(),validUntil:null};this.saving=true;this.api.create(request).subscribe({next:rule=>{this.rules=[...this.rules,rule];this.code='';this.name='';this.lowerLimit=null;this.upperLimit=null;this.saving=false;this.success='Regla creada correctamente.';this.error=''},error:()=>{this.saving=false;this.error='No fue posible crear la regla.'}})}
  protected toggle(rule:AlertRuleDto):void{if(!this.canAdminister()||!window.confirm(`¿Deseas ${rule.isActive?'desactivar':'activar'} esta regla?`))return;this.api.changeStatus(rule.id,!rule.isActive).subscribe({next:updated=>{this.rules=this.rules.map(item=>item.id===updated.id?updated:item);this.success='Estado de la regla actualizado.';this.error=''},error:()=>this.error='No fue posible cambiar el estado de la regla.'})}
}

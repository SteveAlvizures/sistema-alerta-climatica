import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { AlertRuleDto, ClimateVariable, CommunityDto, SensorDto } from '../../../../core/models/api.model';
import { AlertRuleApiService, CreateAlertRuleRequest } from '../../../../core/services/alert-rule-api.service';
import { AuthService } from '../../../../core/services/auth.service';
import { CommunityApiService } from '../../../../core/services/community-api.service';

import { SensorApiService } from '../../../../core/services/sensor-api.service';

const variables:Record<ClimateVariable,string>={Temperature:'Temperatura',RelativeHumidity:'Humedad relativa',WindSpeed:'Velocidad del viento',RainfallLevel:'Nivel de lluvia',RiverOrReservoirLevel:'Nivel de río o reservorio'};
const levels:Record<string,string>={Green:'Normal',Yellow:'Precaución',Orange:'Alerta',Red:'Emergencia'};
const conditions:Record<string,string>={'>':'Mayor que','>=':'Mayor o igual que','<':'Menor que','<=':'Menor o igual que'};
const units:Record<ClimateVariable,string>={Temperature:'°C',RelativeHumidity:'%',WindSpeed:'km/h',RainfallLevel:'mm',RiverOrReservoirLevel:'m'};
const phenomena: Record<AlertRuleDto['phenomenon'], string> = {
  Flood: 'Inundación', Drought: 'Sequía', Storm: 'Tormenta', Frost: 'Helada', Wildfire: 'Incendio forestal',
};
interface RuleFilters { communityId:string; variable:''|ClimateVariable; dangerLevel:''|'Green'|'Yellow'|'Orange'|'Red'; }
const emptyRuleFilters=():RuleFilters=>({communityId:'',variable:'',dangerLevel:''});

@Component({selector:'app-alert-rules-page',imports:[FormsModule],templateUrl:'./alert-rules-page.html',styleUrl:'./alert-rules-page.scss'})
export class AlertRulesPage implements OnInit {
  private readonly sensorsApi=inject(SensorApiService); protected sensors:SensorDto[]=[];
  protected availableSensors():SensorDto[]{return this.sensors.filter(s=>s.communityId===this.communityId&&s.measurementType===this.variable)}
  private readonly api=inject(AlertRuleApiService); private readonly communitiesApi=inject(CommunityApiService); protected readonly auth=inject(AuthService);
  protected rules:AlertRuleDto[]=[]; protected communities:CommunityDto[]=[]; protected loading=true; protected saving=false; protected error=''; protected success='';
  protected editingLegacy=false; protected legacyCondition=''; protected editingId:string|null=null; protected sensorId:string|null=null; protected minValue:number|null=null; protected maxValue:number|null=null; protected message=''; protected validFrom=this.localDate(new Date().toISOString()); protected validUntil='';
  protected communityId=''; protected name=''; protected variable:ClimateVariable='Temperature'; protected phenomenon: '' | AlertRuleDto['phenomenon']=''; protected dangerLevel='Yellow'; protected initialActive=true;
  protected readonly phenomenonOptions = Object.entries(phenomena);
  protected filterDraft:RuleFilters=emptyRuleFilters(); protected activeFilters:RuleFilters=emptyRuleFilters();
  protected filtersApplied=false;
  protected readonly variableOptions=Object.keys(variables) as ClimateVariable[];
  ngOnInit():void{forkJoin({rules:this.api.getAll(),communities:this.communitiesApi.getAll(),sensors:this.sensorsApi.getAll()}).subscribe({next:({rules,communities,sensors})=>{this.sensors=sensors;this.rules=rules;this.communities=communities;this.communityId=communities[0]?.id??'';this.loading=false},error:()=>{this.error='No fue posible cargar las reglas.';this.loading=false}})}
  protected canAdminister():boolean{return this.auth.canOperate()}
  protected communityName(id:string):string{return this.communities.find(item=>item.id===id)?.name??'Comunidad no disponible'}
  protected variableLabel(value:ClimateVariable):string{return variables[value]}
  protected levelLabel(value:string):string{return levels[value]??value}
  protected conditionLabel(value:string):string{return conditions[value]??value}
  protected unit():string{return units[this.variable]}
  protected filteredRules():AlertRuleDto[]{return this.filtersApplied?this.rules.filter(rule=>(!this.activeFilters.communityId||rule.communityId===this.activeFilters.communityId)&&(!this.activeFilters.variable||rule.variable===this.activeFilters.variable)&&(!this.activeFilters.dangerLevel||rule.dangerLevel===this.activeFilters.dangerLevel)):[]}
  protected applyRuleFilters():void{this.activeFilters={...this.filterDraft};this.filtersApplied=true}
  protected clearRuleFilters():void{this.filterDraft=emptyRuleFilters();this.activeFilters=emptyRuleFilters();this.filtersApplied=false}
  protected rangeLabel(rule:AlertRuleDto):string {
    if (!rule.usesRange) return `${this.conditionLabel(rule.comparisonOperator)} ${rule.activationPoint}`;
    return this.formatRange(rule.minValue ?? rule.lowerLimit, rule.maxValue ?? rule.upperLimit);
  }
  protected formatRange(min:number|null,max:number|null):string { return min!==null && max!==null ? `${min} a ${max}` : min!==null ? `${min} o más` : max!==null ? `${max} o menos` : 'Define un rango'; }
  protected phenomenonLabel(value:AlertRuleDto['phenomenon']):string{return phenomena[value]}
  protected preview():string{return `${this.variableLabel(this.variable)}: ${this.formatRange(this.minValue,this.maxValue)} ${this.unit()} (${this.levelLabel(this.dangerLevel)})`}
  protected edit(rule:AlertRuleDto):void {
    this.editingLegacy=!rule.usesRange;this.legacyCondition=this.rangeLabel(rule);this.editingId=rule.id;this.communityId=rule.communityId;this.sensorId=rule.sensorId;this.name=rule.name;this.variable=rule.variable;this.phenomenon=rule.phenomenon;this.dangerLevel=rule.dangerLevel;
    this.minValue=rule.minValue??rule.lowerLimit;this.maxValue=rule.maxValue??rule.upperLimit;this.message=rule.message??'';this.initialActive=rule.isActive;
    this.validFrom=this.localDate(rule.validFrom);this.validUntil=rule.validUntil?this.localDate(rule.validUntil):'';
  }
  private localDate(value:string):string{const d=new Date(value);return new Date(d.getTime()-d.getTimezoneOffset()*60000).toISOString().slice(0,16)}
  protected cancelEdit():void{this.editingLegacy=false;this.editingId=null;this.sensorId=null;this.name='';this.message='';this.minValue=null;this.maxValue=null;this.phenomenon='';this.initialActive=true;this.validFrom=this.localDate(new Date().toISOString());this.validUntil=''}
  protected create():void{
    if(!this.canAdminister()||this.saving)return;
    if (!this.phenomenon || !Object.hasOwn(phenomena,this.phenomenon)){this.error='Selecciona un fenómeno climático válido.';return}
    if(!this.name.trim()||!this.communityId||!Object.hasOwn(variables,this.variable)||!Object.hasOwn(levels,this.dangerLevel)){this.error='Completa nombre, comunidad, variable y nivel.';return}
    if(this.minValue===null&&this.maxValue===null){this.error='Define al menos un valor mínimo o máximo.';return}
    if(this.minValue!==null&&this.maxValue!==null&&this.minValue>this.maxValue){this.error='El mínimo no puede superar el máximo.';return}
    if(!this.validFrom||!Number.isFinite(new Date(this.validFrom).getTime())||(this.validUntil&&(!Number.isFinite(new Date(this.validUntil).getTime())||new Date(this.validUntil)<new Date(this.validFrom)))){this.error='La vigencia no es válida.';return}
    const request:CreateAlertRuleRequest={communityId:this.communityId,sensorId:this.sensorId,code:'',name:this.name.trim(),phenomenon:this.phenomenon,variable:this.variable,dangerLevel:this.dangerLevel as CreateAlertRuleRequest['dangerLevel'],minValue:this.minValue,maxValue:this.maxValue,message:this.message.trim(),isActive:this.initialActive,validFrom:new Date(this.validFrom).toISOString(),validUntil:this.validUntil?new Date(this.validUntil).toISOString():null};
    const editing=this.editingId;
    this.saving=true;(editing?this.api.update(editing,request):this.api.create(request)).subscribe({next:rule=>{this.rules=editing?this.rules.map(item=>item.id===rule.id?rule:item):[...this.rules,rule];this.cancelEdit();this.saving=false;this.success='Regla guardada correctamente.';this.error=''},error:()=>{this.saving=false;this.error='No fue posible guardar la regla.'}})
  }
  protected toggle(rule:AlertRuleDto,ask=true):void{if(!this.canAdminister()||(ask&&!window.confirm(`¿Deseas ${rule.isActive?'desactivar':'activar'} esta regla?`)))return;this.api.changeStatus(rule.id,!rule.isActive).subscribe({next:updated=>{this.rules=this.rules.map(item=>item.id===updated.id?updated:item);this.success='Estado de la regla actualizado.';this.error=''},error:()=>this.error='No fue posible cambiar el estado de la regla.'})}
}

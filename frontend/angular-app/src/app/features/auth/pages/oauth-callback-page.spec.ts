import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { API_BASE_URL } from '../../../core/config/api.config';
import { AuthService } from '../../../core/services/auth.service';
import { OAuthCallbackPage } from './oauth-callback-page';

describe('OAuth callback',()=>{
  beforeEach(()=>{
    sessionStorage.clear();
    TestBed.configureTestingModule({imports:[OAuthCallbackPage],providers:[provideRouter([]),provideHttpClient(withXhr()),provideHttpClientTesting(),{provide:API_BASE_URL,useValue:'/api'}]});
  });
  afterEach(()=>{TestBed.inject(HttpTestingController).verify();history.replaceState({},'','/');sessionStorage.clear();});
  it('removes the ticket, exchanges it and stores the internal session',()=>{
    history.replaceState({},'','/auth/callback#code='+'a'.repeat(43));
    const router=TestBed.inject(Router);spyOn(router,'navigateByUrl').and.resolveTo(true);
    TestBed.createComponent(OAuthCallbackPage);
    expect(location.hash).toBe('');
    const request=TestBed.inject(HttpTestingController).expectOne('/api/auth/oauth/exchange');
    expect(request.request.body).toEqual({code:'a'.repeat(43)});
    request.flush({accessToken:'internal-jwt',expiresAt:new Date(Date.now()+60000).toISOString(),name:'Reader',email:'reader@example.test',role:'VIEWER'});
    expect(TestBed.inject(AuthService).token()).toBe('internal-jwt');expect(TestBed.inject(AuthService).canOperate()).toBeFalse();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/',{replaceUrl:true});
  });
  it('rejects a malformed callback without exchanging a token',()=>{
    history.replaceState({},'','/auth/callback#accessToken=untrusted');
    const fixture=TestBed.createComponent(OAuthCallbackPage);fixture.detectChanges();
    expect(fixture.componentInstance.failed()).toBeTrue();expect(location.hash).toBe('');
  });
  it('shows a safe error on an expired or reused ticket',()=>{
    history.replaceState({},'','/auth/callback#code='+'a'.repeat(43));
    const fixture=TestBed.createComponent(OAuthCallbackPage);
    TestBed.inject(HttpTestingController).expectOne('/api/auth/oauth/exchange').flush({}, {status:401,statusText:'Unauthorized'});
    expect(fixture.componentInstance.failed()).toBeTrue();expect(TestBed.inject(AuthService).token()).toBeNull();
  });
  for(const [role,operate,admin] of [['ADMIN',true,true],['OPERATOR',true,false],['VIEWER',false,false]] as const) {
    it('applies official permissions for '+role,()=>{
      TestBed.inject(AuthService).login({username:'test',password:'test'}).subscribe();
      TestBed.inject(HttpTestingController).expectOne('/api/auth/login').flush({accessToken:'jwt',expiresAt:new Date(Date.now()+60000).toISOString(),name:'Test',email:'test',role});
      expect(TestBed.inject(AuthService).canOperate()).toBe(operate);expect(TestBed.inject(AuthService).canManageUsers()).toBe(admin);
    });
  }
});

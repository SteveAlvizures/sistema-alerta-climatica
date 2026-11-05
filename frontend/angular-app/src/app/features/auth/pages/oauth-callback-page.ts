import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({selector:'app-oauth-callback',imports:[RouterLink],template:`<main class="login"><section class="login__card"><h1>Inicio de sesión</h1><p role="status">{{message()}}</p>@if(failed()){<a routerLink="/login">Volver a iniciar sesión</a>}</section></main>`})
export class OAuthCallbackPage {
  readonly message=signal('Comprobando tu sesión…');
  readonly failed=signal(false);
  private readonly auth=inject(AuthService);
  private readonly router=inject(Router);
  constructor() {
    const params=new URLSearchParams(window.location.hash.slice(1)),codes=params.getAll('code');
    // Remove the ticket immediately, even on malformed/error callbacks.
    history.replaceState(history.state,'',window.location.pathname+window.location.search);
    if(codes.length!==1||!/^[A-Za-z0-9_-]{43}$/.test(codes[0])) {this.fail();return;}
    this.auth.completeOAuth(codes[0]).subscribe({next:()=>void this.router.navigateByUrl('/',{replaceUrl:true}),error:()=>this.fail()});
  }
  private fail() {this.failed.set(true);this.message.set('No fue posible completar el acceso. Vuelve a intentarlo.');}
}

import { HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { map } from 'rxjs';
import { waterPresentation } from '../models/water-presentation';

export const waterPresentationInterceptor: HttpInterceptorFn = (request,next) => next(request).pipe(
  map(event => event instanceof HttpResponse && event.body && typeof event.body === 'object'
    && !(event.body instanceof Blob) && !(event.body instanceof ArrayBuffer)
    ? event.clone({body:waterPresentation(event.body)}) : event),
);

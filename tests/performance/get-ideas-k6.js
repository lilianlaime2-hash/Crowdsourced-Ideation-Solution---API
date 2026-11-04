import { performanceOptions } from './helpers/config.js';
import { setupRead, read } from './helpers/api.js';

export const options = performanceOptions(__ENV, ['get_ideas']);
export function setup() { return setupRead('get_ideas'); }
export default function (session) { read('get_ideas', session); }

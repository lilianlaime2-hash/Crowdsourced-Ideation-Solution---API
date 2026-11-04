import { performanceOptions } from './helpers/config.js';
import { setupRead, read } from './helpers/api.js';

export const options = performanceOptions(__ENV, ['get_topics']);
export function setup() { return setupRead('get_topics'); }
export default function (session) { read('get_topics', session); }

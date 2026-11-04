import { performanceOptions } from './helpers/config.js';
import { setupRead, read } from './helpers/api.js';

export const options = performanceOptions(__ENV, ['get_topic']);
export function setup() { return setupRead('get_topic'); }
export default function (session) { read('get_topic', session); }

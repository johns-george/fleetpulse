import { environment } from '../../environments/environment';

/**
 * Base URL of the FleetPulse API. The rest of the app only reads it from here;
 * local vs hosted is selected by the environment file replacement in angular.json.
 */
export const API_BASE_URL = environment.apiBaseUrl;

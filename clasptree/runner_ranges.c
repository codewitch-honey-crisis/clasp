int adv = 0;
int tlen;
TYPE tto;
TYPE prlen;
TYPE pmin;
TYPE pmax;
int i, j;
int ch;
TYPE state = 0;
TYPE acc = -1;
bool done;
bool result;
ch = (path_and_query[adv]=='\0'||path_and_query[adv]=='?') ? -1 : path_and_query[adv++];
while (ch != -1) {
	result = false;
	acc = -1;
	done = false;
	while (!done) {
	start_dfa:
		done = true;
		acc = fsm_data[state++];
		tlen = fsm_data[state++];
		for (i = 0; i < tlen; ++i) {
			tto = fsm_data[state++];
			prlen = fsm_data[state++];
			for (j = 0; j < prlen; ++j) {
				pmin = fsm_data[state++];
				pmax = fsm_data[state++];
				if (ch < pmin) {
					state += ((prlen - (j + 1)) * 2);
					break;
				}
				if (ch <= pmax) {
					result = true;
					ch = (path_and_query[adv] == '\0' || path_and_query[adv] == '?') ? -1 : path_and_query[adv++];
					state = tto;
					done = false;
					goto start_dfa;
				}
			}
		}
		if (acc != -1 && result) {
			if (path_and_query[adv]=='\0' || path_and_query[adv]=='?') {
				return (int)acc;
			}
			return -1;
		}
		ch = (path_and_query[adv] == '\0' || path_and_query[adv] == '?') ? -1 : path_and_query[adv++];
		state = 0;
	}
}
return -1;


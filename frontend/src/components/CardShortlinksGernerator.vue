<template>
  <v-card elevation="2" height="100%">
    <v-card-title primary-title class="text-h3"> Generator </v-card-title>
    <v-card-text class="text-h4"> Enter URL </v-card-text>
    <v-container>
      <validation-observer ref="observer" v-slot="{ invalid, handleSubmit }">
        <form @submit.prevent="handleSubmit(submit)">
          <validation-provider v-slot="{ errors }" rules="required|url" name="url">
            <v-text-field type="text" label="URL" :error-messages="errors" v-model="url" required> </v-text-field>
          </validation-provider>
          <validation-provider v-slot="{ errors }" rules="min:4|max:64" name="userShortlink">
            <v-text-field type="text" label="Shortlink (optional)" :error-messages="errors" v-model="userShortlink">
            </v-text-field>
          </validation-provider>
          <v-btn min-width="100%" type="submit" color="success" :disabled="invalid"> Generate Shortlink </v-btn>
        </form>
      </validation-observer>
    </v-container>
  </v-card>
</template>

<script lang="ts">
import Vue from "vue";
import { mapGetters, mapActions } from "vuex";
import axios from "axios";
import { ValidationProvider, ValidationObserver } from "vee-validate";

export default Vue.extend({
  components: {
    ValidationProvider,
    ValidationObserver,
  },
  data() {
    return {
      url: "",
      userShortlink: "",
    };
  },
  methods: {
    ...mapGetters(["getShortlinks"]),
    ...mapActions(["fetchShortlinks"]),
    async submit(): Promise<void> {
      try {
        const response = await axios.post(
          "api/link/add",
          {
            targetUrl: this.url,
            shortPath: this.userShortlink ? this.userShortlink : null,
          },
          {
            withCredentials: true,
          }
        );

        if (response.status !== 200) {
          throw response;
        }

        await this.fetchShortlinks();
      } catch (e) {
        console.log(e);
      }
    },
  },
});
</script>

<style scoped></style>

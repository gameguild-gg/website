"use client";

import React from "react";
import Card from "@game-guild/ui/src/card";
import CookieIcon from "./cookie-icon";
import Button from "@game-guild/ui/src/button";
import Label from "@game-guild/ui/src/label";
import Switch from "@game-guild/ui/src/switch";


type CookiePreferencesProps = {};

function CookiePreferences({}: Readonly<CookiePreferencesProps>) {
  return (
    <Card key="1" className="w-full max-w-lg">
      <Card.Header className="border-b border-dark-gray-300 pb-4">
        <div className="flex items-center">
          <CookieIcon className="mr-2" />
          <Card.Title>Cookie Preferences</Card.Title>
        </div>
        <Card.Description>
          Manage your cookie settings. You can enable or disable different types of cookies below.
        </Card.Description>
      </Card.Header>
      <Card.Content className="space-y-4 pt-4">
        <div className="flex justify-between items-start space-y-2">
          <div>
            <Label htmlFor="essential">Essential Cookies</Label>
            <p className="text-dark-gray-500 text-sm">
              These cookies are necessary for the website to function and cannot be switched off.
            </p>
          </div>
          <Switch className="ml-auto" id="essential" />
        </div>
        <div className="flex justify-between items-start space-y-2">
          <div>
            <Label htmlFor="analytics">Analytics Cookies</Label>
            <p className="text-dark-gray-500 text-sm">
              These cookies allow us to count visits and traffic sources, so we can measure and improve the performance
              of our site.
            </p>
          </div>
          <Switch className="ml-auto" id="analytics" />
        </div>
        <div className="flex justify-between items-start space-y-2">
          <div>
            <Label htmlFor="marketing">Marketing Cookies</Label>
            <p className="text-dark-gray-500 text-sm">
              These cookies help us show you relevant ads.
            </p>
          </div>
          <Switch className="ml-auto" id="marketing" />
        </div>
      </Card.Content>
      <div className="border-t border-dark-gray-300 mt-4" />
      <Card.Footer>
        <Button className="ml-auto" type="submit">
          Save Preferences
        </Button>
      </Card.Footer>
    </Card>
  );
}

export default CookiePreferences;
